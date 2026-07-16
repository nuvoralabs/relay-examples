namespace Coordination.Fenced;

/// <summary>
/// A controllable clock. <see cref="InMemoryLeaseStore"/> and <see cref="InMemoryNodeRegistry"/> take an
/// optional <see cref="TimeProvider"/>; injecting this makes lease expiry, fencing-token takeover, and
/// heartbeat liveness deterministic in tests — we call <see cref="Advance"/> instead of sleeping, so a
/// "after the TTL the lease lapses" assertion runs in microseconds and never flakes.
/// </summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private DateTimeOffset _now = DateTimeOffset.UnixEpoch;
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan by) => _now += by;
}
