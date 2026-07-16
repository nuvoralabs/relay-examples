# Sample 027 — Time-Travel & Live Aggregation

Companion to **[Article 027 — Time-Travel & Live Aggregation](https://relay.nuvoralabs.com/articles/time-travel-and-live-aggregation/)**.

The read-side counterpart to event sourcing. A `BankAccount` aggregate is event-sourced exactly as in
[sample 005](../005-event-sourcing-basics), but here nothing is read for *writing* — the
**`IEventSourcedReader<BankAccount, Guid>`** rebuilds historical and current state on demand, straight
from the event stream, with no persisted read model.

## What it shows

- **Time-travel by version** — `LoadAtVersionAsync(id, version)` folds the stream up to (and including)
  a past version, reconstructing the balance as it stood then.
- **Time-travel by timestamp** — `LoadAtTimeAsync(id, asOf)` folds only the events whose timestamp is at
  or before an instant, answering "what was the balance as of *then*."
- **Live aggregation** — `LoadAsync(id)` folds the whole stream to produce current state, with **no
  projection / no read-model table** — and without tracking the aggregate on the unit of work (a pure read).
- **The reader ignores snapshots on purpose** — a snapshot captures the head, which is wrong for a
  time-travel read; replay from zero is always correct for these query-side reads.

The aggregate is the *same apply-only shape from articles 002 and 005* — the reader is a query-side
service, not a domain change.

## Layout (one self-contained test project)

| File | Contents |
|---|---|
| [`Accounts/BankAccount.cs`](Ledger.TimeTravel.Tests/Accounts/BankAccount.cs) | The event-sourced aggregate + its events |
| [`Accounts/Commands.cs`](Ledger.TimeTravel.Tests/Accounts/Commands.cs) | Commands + handlers (write side, no `[SkipTransaction]`) |
| [`LedgerDbContext.cs`](Ledger.TimeTravel.Tests/LedgerDbContext.cs) | `ApplyRelayEventStore()` + `ApplyRelaySnapshots()` |
| [`LedgerFixture.cs`](Ledger.TimeTravel.Tests/LedgerFixture.cs) | Testcontainers Postgres + DI, incl. `AddRelayEventSourcedReaderEfCore()` |
| [`TimeTravelTests.cs`](Ledger.TimeTravel.Tests/TimeTravelTests.cs) | as-of-version, as-of-timestamp, live aggregation, unknown-account |

## Run it

This sample is exercised through its tests, which provision a **real PostgreSQL** with
[Testcontainers](https://dotnet.testcontainers.org/). That is the honest demonstration — reconstructing
state from a stored stream needs a database.

```bash
dotnet test samples/027-time-travel-and-live-aggregation/Ledger.TimeTravel.Tests
```

> **Requires the .NET 10 SDK *and* a running Docker daemon** (Testcontainers starts `postgres:16`).
> To host this in a real service, copy the `LedgerFixture` DI block into your `Program.cs`, point
> `UseNpgsql` at your database, and apply the schema (EF migrations, or the baseline script in
> `libraries/nuvora-nexus-relay/docs/migrations/`).
