# Sample 025 — Reference Architecture (Storefront)

Companion to **[Article 025 — Reference Architecture](https://relay.nuvoralabs.com/articles/reference-architecture/)**.

The capstone: a single service that composes the pieces from the earlier articles into the canonical
Relay shape — **CQRS + event sourcing + the outbox + projections + queries**, all on one PostgreSQL
DbContext so every write is atomic.

One end-to-end test drives the whole flow:

1. **Command → write model.** `PlaceOrderCommand` event-sources an `Order` (appends `OrderPlaced`) **and**
   publishes an `OrderPlacedNotification` — staged on the **outbox** in the *same* transaction (no dual
   write). The test asserts the outbox row is `Pending`.
2. **Projection → read model.** The projection host folds the event stream into an `OrderSummary` row.
3. **Query → read model.** `GetOrderSummaryQuery` reads the summary through the query bus.
4. **Follow-up.** `MarkOrderPaidCommand` appends `OrderPaid`, which projects onto the existing summary
   row (`Status = "Paid"`).

This mirrors the production wiring exactly — the only things the test adds are the PostgreSQL container
and starting the projection host by hand (a real host runs it for the app's lifetime).

## Test it

```bash
dotnet test samples/025-reference-architecture/Storefront.Sample.Tests
```

> Requires the **.NET 10 SDK** and a running **Docker** daemon (PostgreSQL via Testcontainers).
