# Sample 026 — Inline & Multi-Stream Projections

Companion to **[Article 026 — Inline & Multi-Stream Projections](../../docs/articles/026-inline-and-multistream-projections.md)**.

Two read-model techniques that sit alongside the async projection host of article 007:

- **Inline (synchronous, in-commit) projections.** `OrderSummaryInlineProjection : IInlineEventProjection`
  maintains an `order_summaries` read model. Registering it in DI makes the transaction executor apply
  each just-appended event to it **inside the command's own transaction** — so the row commits atomically
  with the events and a `SELECT` immediately after the command sees it (**read-your-writes, no lag, no
  host**). The test asserts the row is present the instant the command returns — no `WaitUntil`, no daemon.
- **Multi-stream projections.** `FulfillmentProjection : MultiStreamProjection` registers typed
  `On<TEvent>` handlers and folds **two streams** — an `Order`'s and its `Shipment`'s (different aggregate
  types) — into one `fulfillments` read-model row keyed by order. The base maps each handled type to its
  stable name, deserializes, and routes to the matching handler; you never switch on raw `EventData`.

## Layout

```
Projections.Inline.Tests/
  Domain/Order.cs                          # event-sourced Order aggregate + events
  Domain/Shipment.cs                       # event-sourced Shipment aggregate + events (a 2nd stream)
  Domain/Commands.cs                       # place/cancel order, dispatch/deliver shipment
  ReadModels/OrderSummaryInlineProjection.cs  # IInlineEventProjection + its read model
  ReadModels/FulfillmentProjection.cs         # MultiStreamProjection + its read model
  ShopDbContext.cs                         # ApplyRelayEventStore() + ApplyRelaySnapshots() + 2 read-model tables
  ShopFixture.cs                           # Testcontainers Postgres + DI (registers the inline projection)
  InlineProjectionTests.cs                 # read-your-writes: assert immediately, no host
  MultiStreamProjectionTests.cs            # typed dispatch folds two streams into one row
```

## Run it

```bash
dotnet test samples/Relay.Samples.slnx           # the whole sample suite
# or just this sample:
dotnet test samples/026-inline-and-multistream-projections/Projections.Inline.Tests
```

> **Requires the .NET 10 SDK and Docker** (Testcontainers starts `postgres:16`).
> To use an inline projection in a real service, copy the DI wiring from `ShopFixture` into your
> `Program.cs` and register your projection with
> `services.AddScoped<IInlineEventProjection, MyInlineProjection>()`. To run a `MultiStreamProjection`
> for real, register it with `services.AddProjection<MyMultiStreamProjection>()` and
> `services.AddRelayProjections()` so the async host (article 007) drives it.
