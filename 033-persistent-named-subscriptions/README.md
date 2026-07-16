# Sample 033 — Persistent Named Subscriptions

Companion to **[Article 033 — Persistent Named Subscriptions](https://relay.nuvoralabs.com/articles/persistent-named-subscriptions/)**.

A **durable, named position** over the global event log. Where a projection checkpoint is a position
*owned by the framework's projection runner*, a named subscription is the same idea exposed for **your
own** catch-up consumer — an external feed, an integration that ships events to another system, a custom
reader — so it **resumes from where it left off across restarts** instead of replaying everything.

The store is `ISubscriptionStore`, backed by PostgreSQL via `EfCoreSubscriptionStore<TContext>` (one row
per name in `relay_subscriptions`). It demonstrates:

- A named subscription starts at `EventCursor.Start` (a full replay) when nothing has been recorded.
- `AdvanceAsync(name, cursor)` **persists** the acknowledged position.
- The position is **monotonic** — a late, duplicated, or out-of-order ack for an earlier place is
  ignored, so it never moves backward.
- **Resume across restart** — a brand-new process (a fresh `ServiceProvider` against the *same*
  database) reads the stored position and resumes from it, rather than re-reading the whole feed.

## Layout (one self-contained test project)

```
Feed.Subscriptions.Tests/
  FeedDbContext.cs                # ApplyRelayEventStore() + ApplyRelaySubscriptions()
  FeedFixture.cs                  # Testcontainers Postgres + AddRelaySubscriptionStorePostgres; builds fresh providers
  PersistentSubscriptionTests.cs  # start-at-0, advance, monotonic, resume-across-restart
```

## Run it

This sample is exercised through its tests, which provision a **real PostgreSQL** with
[Testcontainers](https://dotnet.testcontainers.org/). That is the honest demonstration — "the position
survives the process" is a database-level guarantee, so the test restarts against a real database.

```bash
dotnet test samples/033-persistent-named-subscriptions/Feed.Subscriptions.Tests
```

> **Requires the .NET 10 SDK *and* a running Docker daemon** (Testcontainers starts `postgres:16`).
> To use a durable subscription in a real service, copy the `FeedFixture` DI block into your `Program.cs`,
> point `UseNpgsql` at your database, call `ApplyRelaySubscriptions()` (and `ApplyRelayEventStore()`) in
> your context's `OnModelCreating`, and apply the schema (EF migrations, or the baseline script in
> `libraries/nuvora-nexus-relay/docs/migrations/`).
