# Sample 037 — Operational Event Queries

Companion to **[Article 037 — Operational Event Queries](https://relay.nuvoralabs.com/articles/operational-event-queries/)**.

The event store is an append-only log, **not** a query database — you query *projections*
(see [007 — Projections](https://relay.nuvoralabs.com/articles/projections/)). But during an incident you sometimes
need to scan the raw log for a needle: *"show me every `PaymentFailed` event, in order, so I can see what
happened."* Relay's `IEventStore.QueryEventsAsync(predicate, maxResults, …)` is that one-off forensic
scan — a **default interface method** that pages the global log via `GetAllEventsAsync` and returns
predicate matches in global-position order, capped at `maxResults`.

This sample proves the four properties that make it trustworthy as a diagnostic tool:

- **Position order** — matches come back ordered by global `Position`, even when seeded out of order.
- **`maxResults` cap** — the scan stops once it has collected that many matches.
- **Empty when nothing matches** — no false end-of-stream.
- **Argument guards** — `null` predicate → `ArgumentNullException`; zero/negative `maxResults` →
  `ArgumentOutOfRangeException`.

**No database** — the test uses an in-memory `IEventStore` stub
([`InMemoryEventStore.cs`](./Forensics.EventQueries.Tests/InMemoryEventStore.cs)) that implements only
the paged `GetAllEventsAsync` the default builds on; every other member throws `NotSupportedException`.

> **Default-interface-method note:** `QueryEventsAsync` is a *default interface method*, so it is callable
> **only through an `IEventStore`-typed reference**, never through the concrete stub class. The test's
> `StoreWith(…)` helper returns the store typed as `IEventStore` for exactly this reason — calling the
> method on the concrete type would not compile.

## Source

| File | Shows | DB? |
|---|---|---|
| [`InMemoryEventStore.cs`](./Forensics.EventQueries.Tests/InMemoryEventStore.cs) | in-memory `IEventStore` stub over a `List<EventData>` | No |
| [`OperationalEventQueryTests.cs`](./Forensics.EventQueries.Tests/OperationalEventQueryTests.cs) | position order, `maxResults`, empty, argument guards | No |

## Test it

```bash
dotnet test samples/037-operational-event-queries/Forensics.EventQueries.Tests
```

> Requires the **.NET 10 SDK**. No Docker/database needed.
