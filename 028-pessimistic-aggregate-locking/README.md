# Sample 028 — Pessimistic Per-Aggregate Write Lock

Companion to **[Article 028 — Pessimistic Per-Aggregate Write Lock](../../docs/articles/028-pessimistic-aggregate-locking.md)**.
Prerequisite: **[Article 006 — Concurrency & Snapshots](../../docs/articles/006-concurrency-and-snapshots.md)**.

Optimistic concurrency is the right default for event-sourced aggregates: writers race, the
expected-version check lets exactly one through, and the loser retries. For a genuinely **hot** aggregate
(a busy SKU everyone reserves at once) that retry loop can thrash. Relay ships a pessimistic alternative:
**`IAggregateWriteLock`** / **`DistributedAggregateWriteLock`**, which serializes concurrent writers to the
*same* aggregate id so the second writer **waits** instead of failing and retrying.

It works by taking a **transaction-scoped** distributed lock on a stable, per-aggregate-id resource key
(`relay:aggregate:{type}:{id}`). The lock rides the command's unit-of-work transaction and releases at
commit/rollback. Writers to *different* aggregate ids never contend.

These tests prove the serialization contract directly:

- **`AggregateWriteLockTests`** — two writers to the **same** aggregate id serialize (the second
  `TryAcquireAsync` returns `false`; `AcquireAsync` waits while the first holds it); writers to
  **different** ids do not block each other; and the lock key is **namespaced per aggregate type + id**.
- **`FakeDistributedLock`** — an in-memory [`IDistributedLock`](../../libraries/nuvora-nexus-relay/src/Nuvora.Nexus.Relay.Core/Coordination/IDistributedLock.cs)
  that tracks currently-held resource keys to enforce real mutual exclusion, with an explicit
  `EndTransaction` standing in for the unit-of-work commit/rollback that releases the lock.

> **No database** — uses a fake lock to show the serialization contract. In production the same
> `IAggregateWriteLock` is backed by a Postgres advisory lock (`AddRelayAggregateWriteLock` +
> `AddRelayDistributedLockPostgres`).

## Test it

```bash
dotnet test samples/028-pessimistic-aggregate-locking/Inventory.Locking.Tests
```

> Requires the **.NET 10 SDK**. No Docker/database needed.
