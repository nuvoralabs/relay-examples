using FluentAssertions;
using Nuvora.Nexus.Relay.Projections.Rebuild;
using Xunit;

namespace Projections.AutoSwap;

/// <summary>
/// The auto-swap policy, end to end against a scripted rebuild manager.
///
/// Article 008 describes the <em>operator-driven</em> blue/green rebuild: a human watches progress and
/// calls <see cref="IProjectionRebuildManager.SwapAsync"/> once the shadow has caught up.
/// <see cref="AutomaticProjectionSwapper"/> closes that loop — it starts a shadow rebuild, polls
/// progress, and swaps <em>automatically</em> the moment the rebuild reports
/// <see cref="ProjectionRebuildStatus.Completed"/>; it swaps under no other outcome.
///
/// These tests prove the policy with zero infrastructure: the rebuild manager is a fake that hands back
/// a canned status sequence, and the swapper polls on a 10ms interval so the loop turns over fast and
/// deterministically. A 5s <c>WaitAsync</c> bounds each test so a regression that loops forever fails
/// loudly instead of hanging.
/// </summary>
public sealed class AutomaticProjectionSwapTests
{
    private static AutomaticProjectionSwapper Swapper(FakeRebuildManager manager)
        // pollInterval kept tiny so the poll loop turns over quickly; timeProvider left as system clock.
        => new(manager, timeProvider: null, pollInterval: TimeSpan.FromMilliseconds(10));

    [Fact]
    public async Task Starts_a_rebuild_and_swaps_automatically_once_it_has_caught_up()
    {
        // The shadow runs for a poll, then reports Completed (caught up to the live head).
        var manager = new FakeRebuildManager(ProjectionRebuildStatus.Running, ProjectionRebuildStatus.Completed);

        await Swapper(manager).RunAsync("account-activity").WaitAsync(TimeSpan.FromSeconds(5));

        manager.StartCalls.Should().Be(1, "the swapper kicks off the shadow rebuild itself");
        manager.SwapCalls.Should().Be(1, "reaching Completed triggers the automatic swap — no operator needed");
    }

    [Fact]
    public async Task Does_not_swap_while_the_rebuild_is_still_running()
    {
        // Progress never leaves Running, so there is nothing to swap in yet. We cancel after giving the
        // loop ample time to poll, then assert it never swapped a half-built shadow.
        var manager = new FakeRebuildManager(ProjectionRebuildStatus.Running);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        await Swapper(manager).RunAsync("account-activity", cancellationToken: cts.Token)
                              .WaitAsync(TimeSpan.FromSeconds(5));

        manager.StartCalls.Should().Be(1);
        manager.SwapCalls.Should().Be(0, "a rebuild that is still running must never swap a partial read model");
    }

    [Fact]
    public async Task Does_not_swap_when_the_rebuild_fails()
    {
        var manager = new FakeRebuildManager(ProjectionRebuildStatus.Running, ProjectionRebuildStatus.Failed);

        await Swapper(manager).RunAsync("account-activity").WaitAsync(TimeSpan.FromSeconds(5));

        manager.StartCalls.Should().Be(1);
        manager.SwapCalls.Should().Be(0, "a failed rebuild leaves the live read model untouched");
    }

    [Fact]
    public async Task Does_not_swap_when_the_rebuild_is_cancelled()
    {
        var manager = new FakeRebuildManager(ProjectionRebuildStatus.Running, ProjectionRebuildStatus.Cancelled);

        await Swapper(manager).RunAsync("account-activity").WaitAsync(TimeSpan.FromSeconds(5));

        manager.StartCalls.Should().Be(1);
        manager.SwapCalls.Should().Be(0, "a cancelled rebuild leaves the live read model untouched");
    }

    [Fact]
    public async Task Defaults_to_a_shadow_rebuild_so_the_swap_is_meaningful()
    {
        // No options passed: the swapper must start a Shadow rebuild (an in-place rebuild has nothing to
        // swap). We capture the options the manager was started with.
        var manager = new CapturingRebuildManager();

        await new AutomaticProjectionSwapper(manager, timeProvider: null, pollInterval: TimeSpan.FromMilliseconds(10))
            .RunAsync("account-activity").WaitAsync(TimeSpan.FromSeconds(5));

        manager.StartedWith.Should().NotBeNull();
        manager.StartedWith!.Mode.Should().Be(RebuildMode.Shadow, "blue/green needs a shadow to swap in");
        manager.SwapCalls.Should().Be(1);
    }

    /// <summary>A fake that records the <see cref="RebuildOptions"/> it was started with, then completes.</summary>
    private sealed class CapturingRebuildManager : IProjectionRebuildManager
    {
        public RebuildOptions? StartedWith { get; private set; }
        public int SwapCalls { get; private set; }

        public Task StartRebuildAsync(string projectionName, RebuildOptions? options = null, Guid? tenant = null, CancellationToken cancellationToken = default)
        {
            StartedWith = options;
            return Task.CompletedTask;
        }

        public Task<RebuildProgress?> GetProgressAsync(string projectionName, Guid? tenant = null, CancellationToken cancellationToken = default)
            => Task.FromResult<RebuildProgress?>(
                new RebuildProgress(projectionName, tenant, ProjectionRebuildStatus.Completed, 100, 100, 100));

        public Task CancelRebuildAsync(string projectionName, Guid? tenant = null, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task SwapAsync(string projectionName, Guid? tenant = null, CancellationToken cancellationToken = default)
        {
            SwapCalls++;
            return Task.CompletedTask;
        }
    }
}
