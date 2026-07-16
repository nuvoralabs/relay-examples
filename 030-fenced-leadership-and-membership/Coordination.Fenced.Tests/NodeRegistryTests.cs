using FluentAssertions;
using Nuvora.Nexus.Relay.Core.Coordination;
using Xunit;

namespace Coordination.Fenced;

/// <summary>
/// <see cref="INodeRegistry"/> gives the coordination plane a membership view by heartbeat. A node that
/// calls <see cref="INodeRegistry.HeartbeatAsync"/> appears in
/// <see cref="INodeRegistry.GetLiveNodesAsync"/>, and disappears once its last heartbeat is older than the
/// liveness TTL — proved deterministically by advancing a <see cref="ManualTimeProvider"/>, no database.
/// </summary>
public sealed class NodeRegistryTests
{
    private static readonly TimeSpan LivenessTtl = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task A_heartbeating_node_appears_in_the_live_set()
    {
        var registry = new InMemoryNodeRegistry(LivenessTtl, new ManualTimeProvider());

        await registry.HeartbeatAsync("node-a");

        (await registry.GetLiveNodesAsync()).Should().ContainSingle().Which.Should().Be("node-a");
    }

    [Fact]
    public async Task A_node_disappears_once_its_heartbeat_is_older_than_the_ttl()
    {
        var clock = new ManualTimeProvider();
        var registry = new InMemoryNodeRegistry(LivenessTtl, clock);

        await registry.HeartbeatAsync("node-a");
        (await registry.GetLiveNodesAsync()).Should().ContainSingle("it just heartbeated");

        // node-a stops heartbeating; advance past the liveness window.
        clock.Advance(LivenessTtl + TimeSpan.FromSeconds(1));

        (await registry.GetLiveNodesAsync()).Should().BeEmpty("its last heartbeat is now older than the TTL");
    }

    [Fact]
    public async Task A_fresh_heartbeat_keeps_a_node_live_across_the_window()
    {
        var clock = new ManualTimeProvider();
        var registry = new InMemoryNodeRegistry(LivenessTtl, clock);

        await registry.HeartbeatAsync("node-a");
        clock.Advance(TimeSpan.FromSeconds(20));
        await registry.HeartbeatAsync("node-a"); // refresh before the lapse
        clock.Advance(TimeSpan.FromSeconds(20)); // only 20s since the refresh — still inside the TTL

        (await registry.GetLiveNodesAsync()).Should().ContainSingle().Which.Should().Be("node-a");
    }

    [Fact]
    public async Task The_live_set_lists_every_currently_live_node_sorted()
    {
        var registry = new InMemoryNodeRegistry(LivenessTtl, new ManualTimeProvider());

        await registry.HeartbeatAsync("node-b");
        await registry.HeartbeatAsync("node-a");

        (await registry.GetLiveNodesAsync()).Should().Equal("node-a", "node-b");
    }
}
