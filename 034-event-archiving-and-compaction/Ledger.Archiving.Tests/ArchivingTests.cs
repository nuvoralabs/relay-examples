using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Nuvora.Nexus.Relay.EventStore;
using Nuvora.Nexus.Relay.EventStore.EfCore;
using Nuvora.Nexus.Relay.EventStore.Snapshots;
using Nuvora.Nexus.Relay.UnitOfWork;
using Xunit;

namespace Ledger.Archiving;

/// <summary>
/// Integration coverage for the PostgreSQL <see cref="IEventArchiver"/> (<c>EfCoreEventArchiver</c>):
/// append a stream of several versions, snapshot it at (or after) the intended cut — the archiver
/// refuses to remove history that is not covered by a snapshot, because replay would otherwise start
/// after the missing prefix — then archive the early versions into <c>relay_events_archive</c>, and
/// verify those events are removed from the live <c>relay_events</c> table while the newer ones remain
/// queryable. A fresh aggregate id keeps each test isolated. These are integration tests against real
/// PostgreSQL because the move (copy-then-delete in one transaction) only exists at the database level —
/// mocking the store would test the mock.
/// </summary>
[Collection("archive")]
public sealed class ArchivingTests
{
    private readonly ArchiveFixture _fixture;

    public ArchivingTests(ArchiveFixture fixture) => _fixture = fixture;

    // A minimal raw event for the given stream/version. The payload is arbitrary here — archiving is a
    // storage operation that moves rows by (aggregate_id, version), independent of the event shape.
    private static EventData Event(Guid aggregateId, long version)
        => new(
            Guid.NewGuid(),
            $"Ledger-{aggregateId:N}",
            "Ledger.MoneyMoved",
            aggregateId,
            "ledger.account",
            version,
            position: 0,
            DateTimeOffset.UtcNow,
            "{}");

    private async Task AppendStreamAsync(Guid aggregateId, params long[] versions)
    {
        using var scope = _fixture.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var store = scope.ServiceProvider.GetRequiredService<IEventStore>();

        await unitOfWork.BeginTransactionAsync();
        await store.AppendManyAsync(
            versions.Select(v => Event(aggregateId, v)),
            expectedVersion: null);
        await unitOfWork.CommitAsync();
    }

    // Snapshot the stream at the given version. Archiving a prefix is only safe when a snapshot at
    // (or after) the cut exists — replay starts from the snapshot, so the archived events are no
    // longer needed to rehydrate — and the archiver enforces exactly that precondition.
    private async Task SaveSnapshotAsync(Guid aggregateId, long version)
    {
        using var scope = _fixture.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var snapshots = scope.ServiceProvider.GetRequiredService<ISnapshotStore>();

        await unitOfWork.BeginTransactionAsync();
        await snapshots.SaveAsync(new SnapshotData(
            aggregateId, "ledger.account", version, "{}", DateTimeOffset.UtcNow));
        await unitOfWork.CommitAsync();
    }

    [Fact]
    public async Task Archives_a_streams_old_events_and_leaves_the_newer_ones_live()
    {
        var aggregateId = Guid.NewGuid();

        // A stream with five versions (0..4) in the hot table, snapshotted at the intended cut.
        await AppendStreamAsync(aggregateId, 0, 1, 2, 3, 4);
        await SaveSnapshotAsync(aggregateId, version: 2);

        // Compact everything up to and including version 2 — versions 0, 1, 2 move to the archive.
        var archiver = _fixture.Services.GetRequiredService<IEventArchiver>();
        var moved = await archiver.ArchiveStreamAsync(aggregateId, upToVersionInclusive: 2);

        // Three events were moved out of the live table.
        moved.Should().Be(3);

        // The live table now holds only the un-archived (newer) tail: versions 3 and 4, in order.
        using var scope = _fixture.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IEventStore>();
        var remaining = await store.GetEventsAsync(aggregateId);

        remaining.Select(e => e.Version).Should().Equal(3, 4);
    }

    [Fact]
    public async Task Archiving_the_whole_stream_empties_the_live_table_for_that_aggregate()
    {
        var aggregateId = Guid.NewGuid();
        await AppendStreamAsync(aggregateId, 0, 1, 2);
        await SaveSnapshotAsync(aggregateId, version: 2);

        var archiver = _fixture.Services.GetRequiredService<IEventArchiver>();
        var moved = await archiver.ArchiveStreamAsync(aggregateId, upToVersionInclusive: 2);

        moved.Should().Be(3);

        using var scope = _fixture.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IEventStore>();
        var remaining = await store.GetEventsAsync(aggregateId);

        remaining.Should().BeEmpty("every version was archived");
    }

    [Fact]
    public async Task Archiving_below_the_oldest_version_moves_nothing()
    {
        var aggregateId = Guid.NewGuid();
        await AppendStreamAsync(aggregateId, 0, 1, 2);
        await SaveSnapshotAsync(aggregateId, version: 0);

        // The first version is 0; there is nothing strictly older to archive.
        var archiver = _fixture.Services.GetRequiredService<IEventArchiver>();
        var moved = await archiver.ArchiveStreamAsync(aggregateId, upToVersionInclusive: 0);

        // Version 0 is inclusive, so exactly one event moves; the rest stay live.
        moved.Should().Be(1);

        using var scope = _fixture.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IEventStore>();
        var remaining = await store.GetEventsAsync(aggregateId);

        remaining.Select(e => e.Version).Should().Equal(1, 2);
    }

    [Fact]
    public async Task Archiving_without_a_covering_snapshot_is_refused()
    {
        var aggregateId = Guid.NewGuid();
        await AppendStreamAsync(aggregateId, 0, 1, 2);

        // No snapshot exists at version >= 1: archiving that prefix would leave the stream
        // unloadable (replay would start at version 2 with no state before it), so the archiver
        // refuses instead of irreversibly deleting required history.
        var archiver = _fixture.Services.GetRequiredService<IEventArchiver>();
        var act = () => archiver.ArchiveStreamAsync(aggregateId, upToVersionInclusive: 1);
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*snapshot*");

        // The stream is untouched — nothing was moved or deleted.
        using var scope = _fixture.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IEventStore>();
        var remaining = await store.GetEventsAsync(aggregateId);

        remaining.Select(e => e.Version).Should().Equal(0, 1, 2);
    }
}
