# Sample 022 — Resiliency Primitives

Companion to **[Article 022 — Resiliency](https://relay.nuvoralabs.com/articles/resiliency/)**.

The building blocks Relay uses internally to keep a system standing when a dependency misbehaves —
exposed as small, pure primitives you can use directly. All live in `Nuvora.Nexus.Relay.Core.Resiliency`
and are **fully in-memory and deterministic** (no database, no broker, no wall-clock sleeps):

- **`ConfigurableRetryPolicy`** — Immediate / Interval / Exponential / Incremental backoff with an
  optional jitter fraction; `BuildDelaySchedule` materialises the curve, `Evaluate` decides per attempt.
- **`CircuitBreaker`** — trips `Open` after a failure threshold, fails fast, then recovers through
  `HalfOpen` after a break duration.
- **`TokenBucketRateLimiter`** — burst-then-refill admission control.
- **`ConcurrencyLimiter`** — a leased cap on in-flight work, with idempotent release.

The circuit breaker and rate limiter take an optional `TimeProvider`, so the tests drive time with a
[`ManualTimeProvider`](./Resiliency.Sample.Tests/ManualTimeProvider.cs) instead of sleeping.

## Test it

```bash
dotnet test samples/022-resiliency/Resiliency.Sample.Tests
```

> Requires the **.NET 10 SDK**. No Docker/database needed.
