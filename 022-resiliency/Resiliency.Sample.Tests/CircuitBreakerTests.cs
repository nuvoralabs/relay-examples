using FluentAssertions;
using Nuvora.Nexus.Relay.Core.Resiliency;
using Xunit;

namespace Resiliency.Sample;

/// <summary>
/// A circuit breaker stops hammering a failing dependency: after enough failures it trips <c>Open</c> and
/// rejects calls immediately (fail fast), then after a cool-off allows a single trial call (<c>HalfOpen</c>)
/// whose outcome closes or re-opens it. Time is controlled via <see cref="ManualTimeProvider"/>.
/// </summary>
public sealed class CircuitBreakerTests
{
    private static (CircuitBreaker Breaker, ManualTimeProvider Clock) New(CircuitBreakerOptions options)
    {
        var clock = new ManualTimeProvider();
        return (new CircuitBreaker(options, clock), clock);
    }

    [Fact]
    public void Trips_open_after_the_failure_threshold_and_short_circuits()
    {
        var (breaker, _) = New(new CircuitBreakerOptions { FailureThreshold = 3, FailureRatio = 0 });

        breaker.RecordFailure();
        breaker.RecordFailure();
        breaker.State.Should().Be(CircuitState.Closed);

        breaker.RecordFailure(); // the third failure trips it
        breaker.State.Should().Be(CircuitState.Open);
        breaker.TryEnter().Should().BeFalse("an open circuit rejects calls immediately");
    }

    [Fact]
    public void Recovers_through_half_open_after_the_break_duration()
    {
        var (breaker, clock) = New(new CircuitBreakerOptions
        {
            FailureThreshold = 1,
            FailureRatio = 0,
            BreakDuration = TimeSpan.FromSeconds(15),
        });

        breaker.RecordFailure();
        breaker.State.Should().Be(CircuitState.Open);

        clock.Advance(TimeSpan.FromSeconds(15));
        breaker.TryEnter().Should().BeTrue("the break elapsed, so one trial call is allowed (half-open)");

        breaker.RecordSuccess();
        breaker.State.Should().Be(CircuitState.Closed, "a successful trial closes the circuit");
    }
}
