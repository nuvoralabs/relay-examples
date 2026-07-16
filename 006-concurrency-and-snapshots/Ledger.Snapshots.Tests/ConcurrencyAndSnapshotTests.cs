using FluentAssertions;
using Ledger.Snapshots.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nuvora.Nexus.Relay.Bus;
using Nuvora.Nexus.Relay.EventStore;
using Nuvora.Nexus.Relay.EventStore.EfCore.Entities;
using Xunit;

namespace Ledger.Snapshots;

[Collection("ledger-snapshots")]
public sealed class ConcurrencyAndSnapshotTests
{
    private readonly LedgerFixture _fixture;
    private ICommandBus Bus => _fixture.Services.GetRequiredService<ICommandBus>();

    public ConcurrencyAndSnapshotTests(LedgerFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task A_stale_expected_version_is_rejected_with_a_concurrency_conflict()
    {
        var id = Guid.NewGuid();
        await Bus.Execute<OpenAccountCommand, Account>(new OpenAccountCommand(id, "first"), CancellationToken.None);

        // Opening the same id again appends AccountOpened with expectedVersion -1, but the store already
        // holds version 0 — the optimistic-concurrency check rejects it. (Any two writers who loaded the
        // same version and both try to append are detected the same way.)
        var act = () => Bus.Execute<OpenAccountCommand, Account>(new OpenAccountCommand(id, "duplicate"), CancellationToken.None);
        await act.Should().ThrowAsync<ConcurrencyConflictException>();

        using var scope = _fixture.Services.CreateScope();
        var events = await scope.ServiceProvider.GetRequiredService<IEventStore>()
            .GetEventsAsync(id, 0, CancellationToken.None);
        events.Should().HaveCount(1, "the conflicting command rolled back entirely");
    }

    [Fact]
    public async Task A_snapshot_is_taken_at_the_cadence_and_load_skips_replay()
    {
        var id = Guid.NewGuid();
        await Bus.Execute<OpenAccountCommand, Account>(new OpenAccountCommand(id, "Ada"), CancellationToken.None);   // v0
        await Bus.Execute<DepositCommand, Account>(new DepositCommand(id, 10m), CancellationToken.None);             // v1
        await Bus.Execute<DepositCommand, Account>(new DepositCommand(id, 5m), CancellationToken.None);              // v2 → crosses a multiple of 2

        using var scope = _fixture.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();

        // SnapshotEvery = 2: the command that advanced the account to version 2 wrote a snapshot there.
        var snapshot = await ctx.Set<SnapshotRecord>().AsNoTracking()
            .Where(s => s.AggregateId == id)
            .OrderByDescending(s => s.Version)
            .FirstOrDefaultAsync();
        snapshot.Should().NotBeNull();
        snapshot!.Version.Should().Be(2);

        // Loading uses that head snapshot, so no events replay — proven by the AppliedFromHistory counter.
        var loaded = await scope.ServiceProvider.GetRequiredService<IEventSourcedRepository<Account, Guid>>()
            .GetByIdAsync(id, CancellationToken.None);
        loaded!.Balance.Should().Be(15m);
        loaded.Version.Should().Be(2);
        loaded.AppliedFromHistory.Should().Be(0, "the head snapshot covers every event, so none replay");
    }
}
