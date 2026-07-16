# Sample 030 — Fenced Leadership & Cluster Membership

Companion to **[Article 030 — Fenced Leadership & Cluster Membership](https://relay.nuvoralabs.com/articles/fenced-leadership-and-membership/)**.

The renewable-lease companion to the connection-bound election in
**[023 — Distributed Coordination](https://relay.nuvoralabs.com/articles/distributed-coordination/)**. Where a
connection-bound lock only fails over when a leader's process *dies*, a renewable lease fails over when a
leader merely *stalls*, and hands each new leader a **fencing token** so a resumed stale leader can be
rejected downstream. All three primitives live in `Nuvora.Nexus.Relay.Core.Coordination`.

**No database — deterministic with a controllable clock.** The in-memory implementations take an optional
`TimeProvider`; the tests drive a [`ManualTimeProvider`](./Coordination.Fenced.Tests/ManualTimeProvider.cs)
with `Advance(...)` instead of sleeping, so expiry and liveness are exact and never flake.

## What it shows

- **`ILeaseStore` / `InMemoryLeaseStore`** — `TryAcquireOrRenewAsync` grants the lease (fencing token 1),
  lets the live holder renew it (token unchanged), blocks a different owner while it is live, and — after
  the TTL lapses with no renewal — lets another owner take over with a **strictly higher** fencing token.
- **`IFencedLeaderElector` / `LeaseLeaderElector`** — elects a leader, invokes the work with the current
  fencing token, renews while it runs, and keeps a second instance from running concurrently for the role.
- **`INodeRegistry` / `InMemoryNodeRegistry`** — a node heartbeats and appears in `GetLiveNodesAsync`, and
  disappears once its last heartbeat is older than the liveness TTL.

## Files

| File | Shows | DB? |
|---|---|---|
| [`LeaseStoreTests.cs`](./Coordination.Fenced.Tests/LeaseStoreTests.cs) | acquire, renew, contention, takeover with a higher fencing token | No |
| [`FencedLeaderElectionTests.cs`](./Coordination.Fenced.Tests/FencedLeaderElectionTests.cs) | elected work runs with a token; second instance does not run concurrently | No |
| [`NodeRegistryTests.cs`](./Coordination.Fenced.Tests/NodeRegistryTests.cs) | heartbeat liveness, lapse after the TTL, refresh | No |
| [`ManualTimeProvider.cs`](./Coordination.Fenced.Tests/ManualTimeProvider.cs) | controllable clock for deterministic expiry/liveness | No |

## Test it

```bash
dotnet test samples/030-fenced-leadership-and-membership/Coordination.Fenced.Tests
```

> Requires the **.NET 10 SDK**. No Docker/database needed.
