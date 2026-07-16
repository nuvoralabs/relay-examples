using FluentAssertions;
using Nuvora.Nexus.Relay.Core.Partitioning;
using Xunit;

namespace Coordination.Sample;

/// <summary>
/// To scale a consumer horizontally you split a keyed stream into partitions and give each worker a
/// subset. The mapping must be <strong>stable</strong> — the same key always lands on the same partition,
/// in every process and after every restart — or ordering per key breaks. <see cref="DefaultPartitioner"/>
/// uses FNV-1a over the UTF-8 key (not <c>string.GetHashCode()</c>, which is randomized per process).
/// Pure and DB-free.
/// </summary>
public sealed class PartitioningTests
{
    [Fact]
    public void A_key_always_maps_to_the_same_partition_and_stays_in_range()
    {
        var partitioner = new DefaultPartitioner();

        for (var i = 0; i < 100; i++)
        {
            var key = $"order-{i}";
            var partition = partitioner.GetPartition(key, partitionCount: 8);

            partition.Should().BeInRange(0, 7);
            partitioner.GetPartition(key, 8).Should().Be(partition, "the mapping must be deterministic");
        }
    }

    [Fact]
    public void Keys_spread_across_more_than_one_partition()
    {
        var partitioner = new DefaultPartitioner();

        var used = Enumerable.Range(0, 200)
            .Select(i => partitioner.GetPartition($"k{i}", 8))
            .Distinct()
            .Count();

        used.Should().BeGreaterThan(1, "a good hash distributes keys, not collapses them");
    }
}
