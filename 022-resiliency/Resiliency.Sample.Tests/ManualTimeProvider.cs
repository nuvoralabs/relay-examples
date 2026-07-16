namespace Resiliency.Sample;

/// <summary>
/// A controllable clock. The circuit breaker and token-bucket rate limiter take an optional
/// <see cref="TimeProvider"/>; injecting this makes their time-based behaviour (break duration, token
/// refill) deterministic in tests instead of depending on wall-clock sleeps.
/// </summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset _now = DateTimeOffset.UnixEpoch;
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan by) => _now += by;
}
