# Sample 031 — Intra-Projection Sharding

Companion to **[Article 031 — Intra-Projection Sharding](../../docs/articles/031-intra-projection-sharding.md)**.

Article 008 establishes that a projection has exactly one active worker (the per-checkpoint advisory lock).
That keeps the read model correct, but it also caps a single hot projection's throughput at one worker. This
sample shows how to lift that cap **for one projection** without breaking ordering: split it into parallel
**lanes** and run one worker per lane.

The whole mechanism is a pure primitive — **`ProjectionPartitioner`** (an `IProjectionPartitioner`):

- **`PartitionFor(EventData)`** assigns each event to a lane in `[0, PartitionCount)` by a **stable hash of
  its aggregate id**. All of an aggregate's events therefore land in the same lane, so per-aggregate order is
  preserved; different aggregates fan out across lanes that can run concurrently.
- **`CheckpointName(projection, partition)`** gives each lane its own checkpoint key (`{projection}#p{n}`,
  e.g. `orders#p3`), so a sharded host can run one worker per lane — each coordinated by the existing
  per-checkpoint advisory lock, exactly as in article 008.

It composes an `IPartitioner` from `Nuvora.Nexus.Relay.Core.Partitioning`: the default FNV-1a hash-modulo
`DefaultPartitioner`, or a `ConsistentHashPartitioner` when you expect to resize the lane count (only ~1/N of
aggregates move instead of nearly all).

**No database — pure partitioner logic.** The tests prove lane assignment and checkpoint naming with no host,
no broker, and no Docker:

- every event maps to a lane in range;
- all events for one aggregate map to the same lane (order preserved);
- different aggregates fan out across lanes;
- each lane gets a distinct checkpoint name;
- it composes with a `ConsistentHashPartitioner`;
- the argument guards hold.

## Test it

```bash
dotnet test samples/Relay.Samples.slnx
```

> Requires the **.NET 10 SDK**. No Docker/database needed.
