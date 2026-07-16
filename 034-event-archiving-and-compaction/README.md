# Sample 034 — Event Archiving & Compaction

Companion to **[Article 034 — Event Archiving & Compaction](https://relay.nuvoralabs.com/articles/event-archiving-and-compaction/)**.

An event store is append-only, so the hot `relay_events` table only ever grows. For long-lived streams
that are already covered by a snapshot, you can **compact** the live table by moving a stream's old
events into `relay_events_archive` — history is preserved (the rows still exist, just elsewhere), while
the table that backs every append and replay stays bounded. This sample demonstrates:

- `IEventArchiver` / `EfCoreEventArchiver<TContext>` — the PostgreSQL archiver, registered with
  `AddRelayEventArchivePostgres<TContext>()`.
- `archiver.ArchiveStreamAsync(aggregateId, upToVersionInclusive)` — moves events with `version <= N`
  out of `relay_events` and into `relay_events_archive` in one transaction (copy-then-delete), returning
  the number moved.
- The live table afterwards holds **only the un-archived tail** — `IEventStore.GetEventsAsync` returns
  just the newer events.
- The snapshot precondition is **enforced**: the archiver checks (inside the archive transaction) that a
  `relay_snapshots` row at `version >= N` exists, and throws instead of removing history the stream
  still needs to rehydrate. Save a snapshot covering the cut first, then archive.

The events are moved as raw rows by `(aggregate_id, version)`, independent of the event shape — archiving
is a *storage* operation, not a domain one.

## Layout (one self-contained test project)

```
Ledger.Archiving.Tests/
  ArchiveDbContext.cs   # ApplyRelayEventStore() + ApplyRelaySnapshots() + ApplyRelayEventArchive()
  ArchiveFixture.cs     # Testcontainers Postgres + the production DI wiring (incl. the DbContext factory)
  ArchivingTests.cs     # append a stream, archive a prefix, assert moved count + the live tail
```

## Run it

This sample is exercised through its tests, which provision a **real PostgreSQL** with
[Testcontainers](https://dotnet.testcontainers.org/). That is the honest demonstration — archiving is a
move between two database tables.

```bash
dotnet test samples/034-event-archiving-and-compaction/Ledger.Archiving.Tests
```

> **Requires the .NET 10 SDK *and* a running Docker daemon** (Testcontainers starts `postgres:16`).
> To use this in a real service, copy the `ArchiveFixture` DI block into your `Program.cs`, point
> `UseNpgsql` at your database, map `ApplyRelayEventArchive()` in your `OnModelCreating`, and apply the
> schema (EF migrations, or the baseline script in `libraries/nuvora-nexus-relay/docs/migrations/`).
