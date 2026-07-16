# Sample 007 — Projections

Companion to **[Article 007 — Projections](https://relay.nuvoralabs.com/articles/projections/)**.

The **read side** of event sourcing. The ledger's events drive three independent projections — one
read model per query shape — so the application answers each question with a simple `SELECT` instead
of replaying events:

- `AccountBalanceProjection` maintains `AccountBalanceReadModel` — a flat row per account answering
  "what's the balance?".
- `VipAccountProjection` maintains `VipAccountReadModel` — which accounts hold **over 1M** right now
  (`WHERE is_vip`), and since when. Events carry deltas, so the projection keeps every account's
  running balance as working state to detect threshold crossings (promotion *and* demotion).
- `AccountActivityProjection` maintains `AccountActivityReadModel` — a per-account, per-month
  transaction counter flagging **highly active** accounts (more than 25 transactions in a month) for a
  single indexed dashboard read. Events are bucketed by their `OccurredAt`, so rebuilds land every
  historical event back in its original month.

All three handle events the same way: stage read-model writes on the shared `DbContext` (never calling
`SaveChanges` themselves), and let the host commit each batch atomically with the checkpoint advance.
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
  Projections/AccountBalanceProjection.cs   # balance read model + its IProjection
  Projections/VipAccountProjection.cs       # VIP read model (balance over 1M) + threshold crossings
  Projections/AccountActivityProjection.cs  # per-month activity counter (>25 tx = highly active)
  LedgerDbContext.cs                        # event store + snapshots + projection checkpoints + read models
  LedgerFixture.cs                          # Testcontainers Postgres + read-side DI
  ProjectionTests.cs                        # runs commands, starts the host, waits for catch-up
```

## Run it

```bash
dotnet test samples/007-projections/Ledger.Projections.Tests
```

> **Requires the .NET 10 SDK and Docker** (Testcontainers starts `postgres:16`).
> In a real service, replace the by-hand host with `services.AddRelayProjections()` and one
> `services.AddProjection<T>()` per projection (`AccountBalanceProjection`, `VipAccountProjection`,
> `AccountActivityProjection`), and the host runs for the app's lifetime.
