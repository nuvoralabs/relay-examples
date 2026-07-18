# Sample 023 — Distributed Coordination

Companion to **[Article 023 — Distributed Coordination](https://relay.nuvoralabs.com/articles/distributed-coordination/)**.

How multiple instances of a service cooperate instead of colliding:

- **Partitioning** ([`PartitioningTests`](./Coordination.Sample.Tests/PartitioningTests.cs)) —
  `DefaultPartitioner` maps a key to a partition with stable FNV-1a hashing (not the per-process-randomized
  `string.GetHashCode()`), so the same key always lands on the same worker. **DB-free.**
- **Leader election** ([`LeaderElectionTests`](./Coordination.Sample.Tests/LeaderElectionTests.cs)) —
  `DistributedLockLeaderElector` runs work on exactly one node, retrying until it wins the lock. Driven
  with a scripted `FakeLock`. **DB-free.**
- **Distributed lock** ([`DistributedLockTests`](./Coordination.Sample.Tests/DistributedLockTests.cs)) —
  the real Postgres `pg_advisory_lock`: mutual exclusion across processes, enforced by the database.
  **Requires Docker** (Testcontainers PostgreSQL).

> The same `IDistributedLock` / `ILeaseStore` / `INodeRegistry` contracts also run on ValKey (or any
> Redis-compatible server) via `Nuvora.Nexus.Relay.Coordination.ValKey` —
> `AddRelayValKeyCoordination(...)` + `AddRelayDistributedLockValKey()` etc. — for deployments that
> coordinate on a key-value store instead of PostgreSQL.

## Test it

```bash
dotnet test samples/023-distributed-coordination/Coordination.Sample.Tests
```

> The partitioner and leader-election tests need only the **.NET 10 SDK**. The distributed-lock tests
> spin up PostgreSQL via Testcontainers, so they need a running **Docker** daemon.
