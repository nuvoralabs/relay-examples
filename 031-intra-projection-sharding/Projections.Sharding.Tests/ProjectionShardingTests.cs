using FluentAssertions;
using Nuvora.Nexus.Relay.Core.Partitioning;
using Nuvora.Nexus.Relay.EventStore;
using Nuvora.Nexus.Relay.Projections;
using Xunit;

namespace Projections.Sharding;

/// <summary>
/// Intra-projection sharding splits one hot projection into parallel lanes. <see cref="ProjectionPartitioner"/>
/// assigns each event to a lane by a stable hash of its aggregate id (so all of an aggregate's events share a
/// lane and per-aggregate order is preserved), while different aggregates fan out across lanes. Each lane has
/// its own checkpoint name (<c>{projection}#p{n}</c>), so a sharded host can run one worker per lane.
///
/// These tests are pure — no database, no host — they prove the partitioning logic that the sharded host
/// relies on. See <see cref="Composes_with_a_consistent_hash_partitioner"/> for the resize-friendly variant.
/// </summary>
public sealed class ProjectionShardingTests
{
    /// <summary>An <see cref="EventData"/> for <paramref name="aggregateId"/> — only the aggregate id drives lanes.</summary>
    private static EventData EventFor(Guid aggregateId)
        => new(Guid.NewGuid(), "stream", "OrderPlaced", aggregateId, "Order", version: 0, position: 1, DateTimeOffset.UtcNow, "{}");

    [Fact]
    public void Every_event_maps_to_a_lane_in_range()
    {
        var partitioner = new ProjectionPartitioner(partitionCount: 8);

        // Whatever the aggregate, the lane is always a valid index into the worker pool.
        for (var i = 0; i < 1_000; i++)
        {
            partitioner.PartitionFor(EventFor(Guid.NewGuid())).Should().BeInRange(0, partitioner.PartitionCount - 1);
        }
    }

    [Fact]
    public void All_events_for_one_aggregate_map_to_the_same_lane()
    {
        var partitioner = new ProjectionPartitioner(partitionCount: 8);
        var aggregateId = Guid.NewGuid();

        var first = partitioner.PartitionFor(EventFor(aggregateId));
        var second = partitioner.PartitionFor(EventFor(aggregateId)); // different event, same aggregate
        var third = partitioner.PartitionFor(EventFor(aggregateId));

        // The whole point: an aggregate's events never split across lanes, so one worker applies them in order.
        first.Should().BeInRange(0, 7);
        second.Should().Be(first, "every event of an aggregate must share a lane to preserve per-aggregate order");
        third.Should().Be(first);
    }

    [Fact]
    public void Different_aggregates_fan_out_across_lanes()
    {
        var partitioner = new ProjectionPartitioner(partitionCount: 8);

        // 500 distinct aggregates should not all collapse onto one lane — that's the parallelism we want.
        var lanesUsed = Enumerable.Range(0, 500)
            .Select(_ => partitioner.PartitionFor(EventFor(Guid.NewGuid())))
            .Distinct()
            .ToList();

        lanesUsed.Should().HaveCount(8, "a stable hash of distinct aggregate ids should reach every lane");
    }

    [Fact]
    public void Each_lane_gets_a_distinct_checkpoint_name()
    {
        var partitioner = new ProjectionPartitioner(partitionCount: 4);

        // Each lane checkpoints independently as {projection}#p{n}, so one worker per lane never collides.
        partitioner.CheckpointName("orders", 0).Should().Be("orders#p0");
        partitioner.CheckpointName("orders", 3).Should().Be("orders#p3");

        var names = Enumerable.Range(0, partitioner.PartitionCount)
            .Select(p => partitioner.CheckpointName("orders", p))
            .ToList();

        names.Should().OnlyHaveUniqueItems("each lane needs its own checkpoint (and advisory lock) to run in parallel");
    }

    [Fact]
    public void Composes_with_a_consistent_hash_partitioner()
    {
        // ConsistentHashPartitioner keeps lane assignments stable when the lane count changes (only ~1/N keys move).
        var partitioner = new ProjectionPartitioner(partitionCount: 8, new ConsistentHashPartitioner());
        var aggregateId = Guid.NewGuid();

        partitioner.PartitionFor(EventFor(aggregateId)).Should().BeInRange(0, 7);
        // Still stable per aggregate when composed with consistent hashing.
        partitioner.PartitionFor(EventFor(aggregateId)).Should().Be(partitioner.PartitionFor(EventFor(aggregateId)));
    }

    [Fact]
    public void Guards_arguments()
    {
        // A projection must have at least one lane.
        Action zeroLanes = () => _ = new ProjectionPartitioner(0);
        zeroLanes.Should().Throw<ArgumentOutOfRangeException>();

        var partitioner = new ProjectionPartitioner(partitionCount: 4);

        // A null event has no aggregate id to hash.
        Action nullEvent = () => partitioner.PartitionFor(null!);
        nullEvent.Should().Throw<ArgumentNullException>();

        // A checkpoint name needs a projection name…
        Action emptyName = () => partitioner.CheckpointName("", 0);
        emptyName.Should().Throw<ArgumentException>();

        // …and a lane index inside [0, PartitionCount).
        Action laneOutOfRange = () => partitioner.CheckpointName("orders", 4);
        laneOutOfRange.Should().Throw<ArgumentOutOfRangeException>();
    }
}
