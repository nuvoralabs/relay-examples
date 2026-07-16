using FluentAssertions;
using Ledger.EventSourcing.Accounts;
using Microsoft.Extensions.DependencyInjection;
using Nuvora.Nexus.Relay.Bus;
using Nuvora.Nexus.Relay.Core.Domain;
using Nuvora.Nexus.Relay.EventStore;
using Xunit;

namespace Ledger.EventSourcing;

[Collection("ledger")]
public sealed class EventSourcingTests
{
    private readonly LedgerFixture _fixture;
    private ICommandBus Bus => _fixture.Services.GetRequiredService<ICommandBus>();

    public EventSourcingTests(LedgerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Commands_append_events_and_the_account_rebuilds_by_replay()
    {
        var id = Guid.NewGuid();
        await Bus.Execute<OpenAccountCommand, Account>(new OpenAccountCommand(id, "Ada"), CancellationToken.None);
        await Bus.Execute<DepositCommand, Account>(new DepositCommand(id, 100m), CancellationToken.None);
        await Bus.Execute<WithdrawCommand, Account>(new WithdrawCommand(id, 30m), CancellationToken.None);

        using var scope = _fixture.Services.CreateScope();

        // The store holds the stream — three contiguous events, nothing else.
        var events = await scope.ServiceProvider.GetRequiredService<IEventStore>()
            .GetEventsAsync(id, 0, CancellationToken.None);
        events.Select(e => e.EventType).Should().Equal(
            typeof(AccountOpened).FullName, typeof(MoneyDeposited).FullName, typeof(MoneyWithdrawn).FullName);
        events.Select(e => e.Version).Should().Equal(0, 1, 2);

        // The repository rebuilds current state purely by replaying those events.
        var account = await scope.ServiceProvider.GetRequiredService<IEventSourcedRepository<Account, Guid>>()
            .GetByIdAsync(id, CancellationToken.None);
        account!.Owner.Should().Be("Ada");
        account.Balance.Should().Be(70m);   // 100 deposited − 30 withdrawn
        account.Version.Should().Be(2);
    }

    [Fact]
    public async Task A_rejected_command_appends_no_events_and_leaves_state_intact()
    {
        var id = Guid.NewGuid();
        await Bus.Execute<OpenAccountCommand, Account>(new OpenAccountCommand(id, "Grace"), CancellationToken.None);
        await Bus.Execute<DepositCommand, Account>(new DepositCommand(id, 10m), CancellationToken.None);

        // Overdrawing violates an aggregate invariant; the DomainException rolls the whole command back.
        var act = () => Bus.Execute<WithdrawCommand, Account>(new WithdrawCommand(id, 50m), CancellationToken.None);
        await act.Should().ThrowAsync<DomainException>();

        using var scope = _fixture.Services.CreateScope();
        var events = await scope.ServiceProvider.GetRequiredService<IEventStore>()
            .GetEventsAsync(id, 0, CancellationToken.None);
        events.Should().HaveCount(2, "the overdraw rolled back, leaving only open + deposit");

        var account = await scope.ServiceProvider.GetRequiredService<IEventSourcedRepository<Account, Guid>>()
            .GetByIdAsync(id, CancellationToken.None);
        account!.Balance.Should().Be(10m);
    }

    [Fact]
    public async Task Reading_an_unknown_account_returns_null()
    {
        using var scope = _fixture.Services.CreateScope();
        var account = await scope.ServiceProvider.GetRequiredService<IEventSourcedRepository<Account, Guid>>()
            .GetByIdAsync(Guid.NewGuid(), CancellationToken.None);
        account.Should().BeNull();
    }
}
