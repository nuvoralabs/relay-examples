using Nuvora.Nexus.Relay.Projections.Rebuild;

namespace Projections.AutoSwap;

/// <summary>
/// A scripted, in-memory <see cref="IProjectionRebuildManager"/> — the whole point of this sample.
///
/// The real rebuild manager drives a shadow read model against PostgreSQL and reports progress from
/// the projection's checkpoint versus the head it captured at start. None of that is needed to test
/// the <em>auto-swap policy</em>: that policy only cares about the sequence of statuses progress
/// reports and whether it calls <see cref="SwapAsync"/> at the right moment. So we feed it a canned
/// sequence of <see cref="ProjectionRebuildStatus"/> values (e.g. <c>Running</c> then <c>Completed</c>)
/// and count how many times start and swap were invoked.
///
/// Each <see cref="GetProgressAsync"/> call returns the next status in the script (clamping on the
/// last one so a poll loop that overshoots keeps seeing the terminal state). This mirrors the unit
/// test inside the framework, which fakes the same interface.
/// </summary>
internal sealed class FakeRebuildManager : IProjectionRebuildManager
{
    private readonly IReadOnlyList<ProjectionRebuildStatus> _statuses;
    private int _index;

    public FakeRebuildManager(params ProjectionRebuildStatus[] statuses) => _statuses = statuses;

    /// <summary>How many times the swapper asked us to start a rebuild.</summary>
    public int StartCalls { get; private set; }

    /// <summary>How many times the swapper asked us to swap the shadow in — the behaviour under test.</summary>
    public int SwapCalls { get; private set; }

    public Task StartRebuildAsync(string projectionName, RebuildOptions? options = null, Guid? tenant = null, CancellationToken cancellationToken = default)
    {
        StartCalls++;
        return Task.CompletedTask;
    }

    public Task<RebuildProgress?> GetProgressAsync(string projectionName, Guid? tenant = null, CancellationToken cancellationToken = default)
    {
        // Clamp on the final scripted status so an extra poll keeps seeing the terminal state.
        var status = _statuses[Math.Min(_index, _statuses.Count - 1)];
        _index++;

        // CurrentPosition climbs toward TargetPosition; Completed means caught up.
        var current = status == ProjectionRebuildStatus.Completed ? 100 : 50;
        return Task.FromResult<RebuildProgress?>(
            new RebuildProgress(projectionName, tenant, status, current, 100, current));
    }

    public Task CancelRebuildAsync(string projectionName, Guid? tenant = null, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SwapAsync(string projectionName, Guid? tenant = null, CancellationToken cancellationToken = default)
    {
        SwapCalls++;
        return Task.CompletedTask;
    }
}
