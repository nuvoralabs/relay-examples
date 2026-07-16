# Sample 008 — Projection Operations

Companion to **[Article 008 — Projection Operations](../../docs/articles/008-projection-operations.md)**.

What keeps a projection healthy in production: surviving a **poison event** and **rebuilding** a read
model from history.

- **Poison handling.** `AccountActivityProjection` throws for accounts named `"POISON"`. After
  `MaxEventRetries` attempts the host moves the event to the **dead-letter store** and advances past it,
  so one bad event can't wedge the stream — proven by a later account still projecting.
- **Rebuild.** After the read model is corrupted, `ProjectionManager.ResetAsync(name, fromPosition: 0)`
  rewinds the projection's checkpoint; the running host replays history and the idempotent projection
  repopulates the read model.

Both patterns mirror the framework's own integration tests.

## Layout

```
Ledger.ProjectionOps.Tests/
  Accounts/Account.cs                       # minimal event-sourced account
  Projections/AccountActivityProjection.cs  # throws for "POISON"; idempotent insert
  LedgerDbContext.cs                        # event store + checkpoints (+ dead-letters/failures)
  LedgerFixture.cs                          # Testcontainers Postgres + read-side DI
  ProjectionOperationsTests.cs              # the poison-skip + the reset/rebuild
```

## Run it

```bash
dotnet test samples/008-projection-operations/Ledger.ProjectionOps.Tests
```

> **Requires the .NET 10 SDK and Docker** (Testcontainers starts `postgres:16`). These tests start and
> stop the projection host by hand and poll for catch-up, so allow them a little time.
