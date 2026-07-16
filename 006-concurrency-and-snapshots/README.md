# Sample 006 — Concurrency & Snapshots

Companion to **[Article 006 — Concurrency & Snapshots](../../docs/articles/006-concurrency-and-snapshots.md)**.

Extends the article-005 ledger with the two tools that make event sourcing practical at scale:

- **Optimistic concurrency.** Appending events asserts the aggregate's expected version; a stale write
  is rejected with `ConcurrencyConflictException` and the whole command rolls back. The test forces a
  deterministic conflict (opening the same account id twice).
- **Snapshots.** With `SnapshotEvery = 2`, a command that advances an account across a version boundary
  writes an `AccountSnapshot`. On load the repository restores the snapshot and replays only the events
  *after* it — proven by the `AppliedFromHistory` counter staying `0` when a head snapshot covers the
  whole stream. The `Account` implements `ISnapshotable`.

## Layout

```
Ledger.Snapshots.Tests/
  Accounts/Account.cs              # event-sourced account + ISnapshotable + AppliedFromHistory counter
  Accounts/Commands.cs             # open + deposit
  LedgerDbContext.cs               # ApplyRelayEventStore() + ApplyRelaySnapshots()
  LedgerFixture.cs                 # Testcontainers Postgres; AddRelayEventStoreEfCore(s => s.SnapshotEvery = 2)
  ConcurrencyAndSnapshotTests.cs   # the conflict + the snapshot cadence/replay-skip
```

## Run it

```bash
dotnet test samples/006-concurrency-and-snapshots/Ledger.Snapshots.Tests
```

> **Requires the .NET 10 SDK and Docker** (Testcontainers starts `postgres:16`).
