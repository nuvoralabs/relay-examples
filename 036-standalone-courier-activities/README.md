# Sample 036 — Standalone Courier Activities

Companion to **[Article 036 — Standalone Courier Activities](../../docs/articles/036-standalone-courier-activities.md)**.

A bounded, in-process distributed transaction without a full saga. A `CourierExecutor` runs an
ordered itinerary of `ICourierActivity` steps (here: reserve stock, charge the card, book a
courier). Each step runs forward in order; if any step fails, the steps that already completed are
compensated in **reverse (LIFO)** order, and the `CourierResult` reports the failure. The failing
step and the not-yet-run steps are never compensated.

Where the routing slip in [017](../017-routing-slips) records compensations onto a durable saga
itinerary, the courier here orchestrates the sequence directly in process — no state machine, no
scheduler, no outbox.

No database — in-memory activities recording execute/compensate calls.

## Run it

```bash
dotnet test samples/036-standalone-courier-activities/Logistics.Courier.Tests
```

> Requires the **.NET 10 SDK**. No Docker/database needed.
