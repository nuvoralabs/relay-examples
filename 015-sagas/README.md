# Sample 015 — Sagas

Companion to **[Article 015 — Sagas](https://relay.nuvoralabs.com/articles/sagas/)**.

An imperative process manager (`OrderSaga`): an `OrderPlaced` event starts a saga (correlated by order
id), a `PaymentReceived` event completes it and cancels its timeout, and an `OrderExpired` timeout
completes it otherwise. Driven entirely in-memory through the real `SagaCoordinator` with fakes — exactly
how the framework unit-tests sagas — so it's fast and needs no database.

## Run it

```bash
dotnet test samples/015-sagas/Sagas.Sample.Tests
```

> Requires the **.NET 10 SDK**. No Docker/database needed. In production, replace the in-memory harness
> with `AddRelaySagas()` + `AddRelaySagasEfCore()` (PostgreSQL) + `AddRelaySaga<OrderSaga, OrderState>()`.
