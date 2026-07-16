using FluentAssertions;
using Ledger.TimeTravel.Accounts;
using Microsoft.Extensions.DependencyInjection;
using Nuvora.Nexus.Relay.Bus;
using Nuvora.Nexus.Relay.EventStore;
using Xunit;

namespace Ledger.TimeTravel;

/// <summary>
/// Proves the two read-side capabilities of <see cref="IEventSourcedReader{TAggregate, TId}"/> against a
/// real PostgreSQL event store:
/// <list type="bullet">
///   <item><b>Time-travel</b> — after several appends, the reader rebuilds the aggregate as of an earlier
///   <em>version</em> and as of an earlier <em>timestamp</em>, and the rebuilt state matches what it was
///   then (not what it is now).</item>
///   <item><b>Live aggregation</b> — the reader returns current state by folding the whole stream on
///   demand, with no persisted read model and without tracking the aggregate for writing.</item>
/// </list>
/// </summary>
[Collection("timetravel")]
public sealed class TimeTravelTests
{
    private readonly LedgerFixture _fixture;

    private ICommandBus Bus => _fixture.Services.GetRequiredService<ICommandBus>();

    public TimeTravelTests(LedgerFixture fixture) => _fixture = fixture;

    private IEventSourcedReader<BankAccount, Guid> Reader(IServiceScope scope)
        => scope.ServiceProvider.GetRequiredService<IEventSourcedReader<BankAccount, Guid>>();

    [Fact]
    public async Task LoadAtVersion_rebuilds_the_state_as_of_an_earlier_version()
    {
        var id = Guid.NewGuid();
        await Bus.Execute<OpenAccountCommand, BankAccount>(new OpenAccountCommand(id, "Ada"), CancellationToken.None); // v0, balance 0
        await Bus.Execute<DepositCommand, BankAccount>(new DepositCommand(id, 100m), CancellationToken.None);          // v1, balance 100
        await Bus.Execute<WithdrawCommand, BankAccount>(new WithdrawCommand(id, 30m), CancellationToken.None);         // v2, balance 70
        await Bus.Execute<DepositCommand, BankAccount>(new DepositCommand(id, 10m), CancellationToken.None);           // v3, balance 80

        using var scope = _fixture.Services.CreateScope();
        var reader = Reader(scope);

        // As of v1 the balance was 100 — the withdrawal and the second deposit had not happened yet.
        var atV1 = await reader.LoadAtVersionAsync(id, 1, CancellationToken.None);
        atV1!.Balance.Should().Be(100m);
        atV1.Version.Should().Be(1);

        // As of v2 the balance was 70 — after the withdrawal, before the second deposit.
        var atV2 = await reader.LoadAtVersionAsync(id, 2, CancellationToken.None);
        atV2!.Balance.Should().Be(70m);
        atV2.Version.Should().Be(2);

        // Asking for a version at or beyond the head is just the current state.
        var atHead = await reader.LoadAtVersionAsync(id, 99, CancellationToken.None);
        atHead!.Balance.Should().Be(80m);
        atHead.Version.Should().Be(3);
    }

    [Fact]
    public async Task LoadAtTime_rebuilds_the_state_as_it_stood_at_an_earlier_instant()
    {
        var id = Guid.NewGuid();
        await Bus.Execute<OpenAccountCommand, BankAccount>(new OpenAccountCommand(id, "Grace"), CancellationToken.None);
        await Bus.Execute<DepositCommand, BankAccount>(new DepositCommand(id, 100m), CancellationToken.None);

        // Capture a boundary between the first deposit and the later activity. The small delays bracket
        // the cutoff so the event timestamps (each event's OccurredAt, persisted by the store) fall
        // unambiguously on either side of it — no reliance on sub-millisecond ordering.
        await Task.Delay(50);
        var asOf = DateTimeOffset.UtcNow;
        await Task.Delay(50);

        await Bus.Execute<WithdrawCommand, BankAccount>(new WithdrawCommand(id, 40m), CancellationToken.None); // after the cutoff
        await Bus.Execute<DepositCommand, BankAccount>(new DepositCommand(id, 5m), CancellationToken.None);    // after the cutoff

        using var scope = _fixture.Services.CreateScope();
        var reader = Reader(scope);

        // As of the captured instant only Open + the first deposit had happened: balance 100.
        var asOfState = await reader.LoadAtTimeAsync(id, asOf, CancellationToken.None);
        asOfState!.Balance.Should().Be(100m);
        asOfState.Version.Should().Be(1);

        // "Now" reflects every event: 100 − 40 + 5 = 65.
        var now = await reader.LoadAsync(id, CancellationToken.None);
        now!.Balance.Should().Be(65m);
        now.Version.Should().Be(3);
    }

    [Fact]
    public async Task Load_does_live_aggregation_with_no_persisted_read_model()
    {
        var id = Guid.NewGuid();
        await Bus.Execute<OpenAccountCommand, BankAccount>(new OpenAccountCommand(id, "Edsger"), CancellationToken.None);
        await Bus.Execute<DepositCommand, BankAccount>(new DepositCommand(id, 250m), CancellationToken.None);
        await Bus.Execute<WithdrawCommand, BankAccount>(new WithdrawCommand(id, 50m), CancellationToken.None);

        using var scope = _fixture.Services.CreateScope();

        // The only thing stored is the event stream — there is no balances table, no projection.
        var events = await scope.ServiceProvider.GetRequiredService<IEventStore>()
            .GetEventsAsync(id, 0, CancellationToken.None);
        events.Select(e => e.Version).Should().Equal(0, 1, 2);

        // The reader folds that stream on demand to produce current state — and never tracks the result
        // for writing, so it is a pure read (LoadAsync is equivalent to LoadAtVersionAsync at the head).
        var account = await Reader(scope).LoadAsync(id, CancellationToken.None);
        account!.Owner.Should().Be("Edsger");
        account.Balance.Should().Be(200m); // 250 deposited − 50 withdrawn
        account.Version.Should().Be(2);
    }

    [Fact]
    public async Task Reading_an_unknown_account_returns_null()
    {
        using var scope = _fixture.Services.CreateScope();
        var reader = Reader(scope);

        (await reader.LoadAsync(Guid.NewGuid(), CancellationToken.None)).Should().BeNull();
        (await reader.LoadAtVersionAsync(Guid.NewGuid(), 5, CancellationToken.None)).Should().BeNull();
        (await reader.LoadAtTimeAsync(Guid.NewGuid(), DateTimeOffset.UtcNow, CancellationToken.None)).Should().BeNull();
    }
}
