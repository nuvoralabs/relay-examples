# Sample 007 — Projections

Companion to **[Article 007 — Projections](../../docs/articles/007-projections.md)**.

The **read side** of event sourcing. The ledger's events drive a projection that maintains an
`AccountBalanceReadModel` — a flat, query-optimised row per account — so the application can answer
"what's the balance?" with a simple `SELECT` instead of replaying events.

- `AccountBalanceProjection : IProjection` handles `AccountOpened` / `MoneyDeposited` / `MoneyWithdrawn`
  and stages read-model writes on the shared `DbContext` (never calling `SaveChanges` itself).
- The `ProjectionHostedService` catches the read model up from the event stream, committing each
  batch's writes together with the projection's checkpoint advance (so the checkpoint can never get
  ahead of the data). The test starts the host by hand and waits for the read model.
- `AddRelayProjectionCheckpointsEfCore<LedgerDbContext>()` + `ApplyRelayProjectionCheckpoints()` provide
  durable checkpoints.

## Layout

```
Ledger.Projections.Tests/
  Accounts/Account.cs                       # the event-sourced aggregate
  Accounts/Commands.cs                      # open / deposit / withdraw
  Projections/AccountBalanceProjection.cs   # the read model + the IProjection
  LedgerDbContext.cs                        # event store + snapshots + projection checkpoints + read model
  LedgerFixture.cs                          # Testcontainers Postgres + read-side DI
  ProjectionTests.cs                        # runs commands, starts the host, waits for catch-up
```

## Run it

```bash
dotnet test samples/007-projections/Ledger.Projections.Tests
```

> **Requires the .NET 10 SDK and Docker** (Testcontainers starts `postgres:16`).
> In a real service, replace the by-hand host with `services.AddRelayProjections()` and
> `services.AddProjection<AccountBalanceProjection>()`, and the host runs for the app's lifetime.
