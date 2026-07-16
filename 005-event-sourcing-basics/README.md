# Sample 005 — Event Sourcing Basics

Companion to **[Article 005 — Event Sourcing Basics](../../docs/articles/005-event-sourcing-basics.md)**.

The first **infrastructure-backed** sample. A bank-ledger `Account` aggregate is event-sourced: nothing
but its stream of events (`AccountOpened`, `MoneyDeposited`, `MoneyWithdrawn`) is stored, and current
state is rebuilt by replaying them. It demonstrates:

- The transactional pipeline (no more `[SkipTransaction]`): each command runs in a DB transaction that
  **appends the aggregate's new events and commits atomically**.
- `IEventSourcedRepository<Account, Guid>` — `GetByIdAsync` rehydrates by replay; `Update` tracks.
- The event store as the source of truth (`IEventStore.GetEventsAsync`).
- A rejected command (overdraw) rolling back so **no events are appended**.

The aggregate is the *same apply-only shape from article 002* — event sourcing is a persistence
decision, not a domain rewrite.

## Layout (one self-contained test project)

```
Ledger.EventSourcing.Tests/
  Accounts/Account.cs    # the event-sourced aggregate + its events
  Accounts/Commands.cs   # commands + handlers (using IEventSourcedRepository)
  LedgerDbContext.cs     # ApplyRelayEventStore() + ApplyRelaySnapshots()
  LedgerFixture.cs       # Testcontainers Postgres + the production DI wiring
  EventSourcingTests.cs  # append/replay, rollback, unknown-account
```

## Run it

This sample is exercised through its tests, which provision a **real PostgreSQL** with
[Testcontainers](https://dotnet.testcontainers.org/). That is the honest demonstration — event sourcing
needs a database.

```bash
dotnet test samples/005-event-sourcing-basics/Ledger.EventSourcing.Tests
```

> **Requires the .NET 10 SDK *and* a running Docker daemon** (Testcontainers starts `postgres:16`).
> To host this domain in a real service, copy the `LedgerFixture` DI block into your `Program.cs`,
> point `UseNpgsql` at your database, and apply the schema (EF migrations, or the baseline script in
> `libraries/nuvora-nexus-relay/docs/migrations/`).
