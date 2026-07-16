using FluentAssertions;
using Nuvora.Nexus.Relay.Core.Resiliency;
using Xunit;

namespace Resiliency.Sample;

/// <summary>
/// <see cref="ConfigurableRetryPolicy"/> turns <see cref="RetryPolicyOptions"/> into a backoff curve.
/// It is pure: same inputs, same delays — which is exactly why the framework also uses it to precompute
/// the outbox/scheduler retry schedule. No clock, no I/O.
/// </summary>
public sealed class RetryPolicyTests
{
    private static ConfigurableRetryPolicy Exponential(double @base = 2, double max = 300)
        => new(new RetryPolicyOptions
        {
            Strategy = RetryStrategy.Exponential,
            BaseDelaySeconds = @base,
            MaxDelaySeconds = max,
        });

    [Fact]
    public void Exponential_backoff_doubles_each_attempt()
        => Exponential().BuildDelaySchedule(4).Should().Equal(new[] { 2d, 4d, 8d, 16d });

    [Fact]
    public void Backoff_is_capped_at_the_configured_maximum()
        => Exponential(@base: 2, max: 10).BuildDelaySchedule(6).Should().Equal(new[] { 2d, 4d, 8d, 10d, 10d, 10d });

    [Fact]
    public void Evaluate_returns_the_delay_for_an_attempt_within_budget()
    {
        var policy = Exponential();
        policy.Evaluate(1, maxAttempts: 5, RetryContext.ForAttempt(1)).Delay.Should().Be(TimeSpan.FromSeconds(2));
        policy.Evaluate(3, maxAttempts: 5, RetryContext.ForAttempt(3)).Delay.Should().Be(TimeSpan.FromSeconds(8));
    }

    [Fact]
    public void Evaluate_stops_once_the_attempt_budget_is_exhausted()
    {
        var policy = Exponential();
        policy.Evaluate(5, maxAttempts: 5, RetryContext.ForAttempt(5)).ShouldRetry.Should().BeTrue();
        policy.Evaluate(6, maxAttempts: 5, RetryContext.ForAttempt(6)).Should().Be(RetryDecision.Stop);
    }
}
