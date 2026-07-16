using FluentAssertions;
using Nuvora.Nexus.Relay.Core.Resiliency;
using Xunit;

namespace Resiliency.Sample;

/// <summary>
/// A concurrency limiter caps how many operations run at once. <c>TryAcquireAsync</c> returns a lease
/// (or null when full); disposing the lease frees the permit. Dispose is idempotent, so a permit can
/// never be over-released.
/// </summary>
public sealed class ConcurrencyLimiterTests
{
    [Fact]
    public async Task A_lease_caps_concurrency_and_is_released_on_dispose()
    {
        var limiter = new ConcurrencyLimiter(maxConcurrency: 1);

        var first = await limiter.TryAcquireAsync();
        first.Should().NotBeNull();
        (await limiter.TryAcquireAsync()).Should().BeNull("the single permit is already in use");

        first!.Dispose();
        limiter.InUse.Should().Be(0);
        (await limiter.TryAcquireAsync()).Should().NotBeNull("the permit was released");
    }

    [Fact]
    public async Task Double_dispose_never_over_releases()
    {
        var limiter = new ConcurrencyLimiter(maxConcurrency: 1);

        var lease = await limiter.TryAcquireAsync();
        lease!.Dispose();
        lease.Dispose(); // must be a no-op

        limiter.InUse.Should().Be(0, "the permit count must never go negative");
        (await limiter.TryAcquireAsync()).Should().NotBeNull();
    }
}
