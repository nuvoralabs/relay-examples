using FluentAssertions;
using Nuvora.Nexus.Relay.Core.Coordination;
using Xunit;

namespace Coordination.Fenced;

/// <summary>
/// A renewable lease is the heart of fenced election. <see cref="ILeaseStore.TryAcquireOrRenewAsync"/>
/// grants the lease to the first caller (with fencing token 1), lets the live holder renew it (token
/// unchanged), blocks a different owner while it is live, and — once it lapses after the TTL with no
/// renewal — lets another node take over with a <em>strictly higher</em> fencing token, so a stalled
/// old leader can be fenced out downstream. Driven by a <see cref="ManualTimeProvider"/> — no database.
/// </summary>
public sealed class LeaseStoreTests
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task First_acquire_succeeds_with_a_fencing_token()
    {
        var store = new InMemoryLeaseStore(new ManualTimeProvider());

        var result = await store.TryAcquireOrRenewAsync("reports", "node-a", Ttl);

        result.Acquired.Should().BeTrue();
        result.FencingToken.Should().Be(1, "the very first holder gets token 1");
    }

    [Fact]
    public async Task The_live_holder_can_renew_with_the_same_token()
    {
        var clock = new ManualTimeProvider();
        var store = new InMemoryLeaseStore(clock);

        var first = await store.TryAcquireOrRenewAsync("reports", "node-a", Ttl);
        clock.Advance(TimeSpan.FromSeconds(10)); // still well within the TTL
        var renew = await store.TryAcquireOrRenewAsync("reports", "node-a", Ttl);

        renew.Acquired.Should().BeTrue();
        renew.FencingToken.Should().Be(first.FencingToken, "a renewal by the live holder must not move the token");
        renew.ExpiresAt.Should().BeAfter(first.ExpiresAt, "renewing extends the lease");
    }

    [Fact]
    public async Task A_second_owner_is_blocked_while_the_lease_is_live()
    {
        var store = new InMemoryLeaseStore(new ManualTimeProvider());

        await store.TryAcquireOrRenewAsync("reports", "node-a", Ttl);
        var contender = await store.TryAcquireOrRenewAsync("reports", "node-b", Ttl);

        contender.Acquired.Should().BeFalse("node-a holds a live lease, so node-b is denied");
    }

    [Fact]
    public async Task After_the_ttl_lapses_a_second_owner_takes_over_with_a_higher_token()
    {
        var clock = new ManualTimeProvider();
        var store = new InMemoryLeaseStore(clock);

        var a = await store.TryAcquireOrRenewAsync("reports", "node-a", Ttl);

        // node-a stalls: it neither renews nor releases. Once the TTL elapses, the lease is up for grabs.
        clock.Advance(Ttl + TimeSpan.FromSeconds(1));

        var b = await store.TryAcquireOrRenewAsync("reports", "node-b", Ttl);

        b.Acquired.Should().BeTrue("the lapsed lease is free to take over");
        b.FencingToken.Should().BeGreaterThan(a.FencingToken,
            "a new holder must get a strictly higher token so a resumed stale node-a is fenced out");
    }
}
