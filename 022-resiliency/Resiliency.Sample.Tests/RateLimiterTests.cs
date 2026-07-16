using FluentAssertions;
using Nuvora.Nexus.Relay.Core.Resiliency;
using Xunit;

namespace Resiliency.Sample;

/// <summary>
/// A token-bucket rate limiter admits a burst up to the bucket size, then refills at
/// <c>PermitsPerWindow / Window</c>. With a controlled clock the refill is exact and testable.
/// </summary>
public sealed class RateLimiterTests
{
    [Fact]
    public void Allows_a_burst_then_rejects_until_the_bucket_refills()
    {
        var clock = new ManualTimeProvider();
        var limiter = new TokenBucketRateLimiter(
            new RateLimiterOptions { PermitsPerWindow = 10, Window = TimeSpan.FromSeconds(1) }, clock);

        for (var i = 0; i < 10; i++)
        {
            limiter.TryAcquire().Should().BeTrue($"permit {i} is within the initial burst");
        }

        limiter.TryAcquire().Should().BeFalse("the bucket is empty");

        clock.Advance(TimeSpan.FromMilliseconds(550)); // refills ~5.5 tokens at 10/sec
        for (var i = 0; i < 5; i++)
        {
            limiter.TryAcquire().Should().BeTrue($"refilled token {i}");
        }

        limiter.TryAcquire().Should().BeFalse("only ~5.5 tokens were refilled");
    }
}
