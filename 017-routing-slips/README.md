# Sample 017 — Routing Slips & Compensation

Companion to **[Article 017 — Routing Slips & Compensation](../../docs/articles/017-routing-slips.md)**.

A distributed transaction without 2PC. A `BookingSaga` books a flight, a hotel, and a car across
services; each forward leg `RecordCompensation`s the command that would undo it. On `TripFailed`,
`Compensate()` replays the recorded compensations in **reverse (LIFO)** order as reliable, scheduled
commands — and a redelivered failure compensates nothing new (the committed itinerary position makes
rollback idempotent). DB-free in-memory harness.

## Run it

```bash
dotnet test samples/017-routing-slips/Sagas.RoutingSlip.Tests
```

> Requires the **.NET 10 SDK**. No Docker/database needed.
