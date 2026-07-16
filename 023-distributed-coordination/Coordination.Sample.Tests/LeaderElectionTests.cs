using FluentAssertions;
using Nuvora.Nexus.Relay.Core.Coordination;
using Xunit;

namespace Coordination.Sample;

/// <summary>
/// Some work must run on exactly one node (a poller, a rebalancer). <see cref="DistributedLockLeaderElector"/>
/// turns a distributed lock into leader election: it contends for <c>leader:&lt;role&gt;</c>, runs the
/// elected work while it holds the lock, and re-contends after a back-off when it does not. Driven here
/// with a scripted <see cref="FakeLock"/> — no database.
/// </summary>
public sealed class LeaderElectionTests
{
    [Fact]
    public async Task The_elected_work_runs_once_leadership_is_held()
    {
        var elector = new DistributedLockLeaderElector(new FakeLock(true), TimeSpan.FromMilliseconds(1));
        using var cts = new CancellationTokenSource();
        var ran = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var task = elector.RunAsync("reports", async ct =>
        {
            ran.TrySetResult();
            await Task.Delay(Timeout.Infinite, ct);
        }, cts.Token);

        (await Task.WhenAny(ran.Task, Task.Delay(5000))).Should().Be(ran.Task, "leadership was granted, so the work runs");

        cts.Cancel();
        await task; // clean shutdown: the cancellation is swallowed
    }

    [Fact]
    public async Task It_retries_until_leadership_is_acquired()
    {
        var fake = new FakeLock(false, false, true); // deny, deny, then grant
        var elector = new DistributedLockLeaderElector(fake, TimeSpan.FromMilliseconds(1));
        using var cts = new CancellationTokenSource();
        var ran = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var task = elector.RunAsync("reports", ct =>
        {
            ran.TrySetResult();
            cts.Cancel();
            return Task.CompletedTask;
        }, cts.Token);

        (await Task.WhenAny(ran.Task, Task.Delay(5000))).Should().Be(ran.Task);
        await task;

        fake.AcquireAttempts.Should().BeGreaterThanOrEqualTo(3, "it retried past the two denials");
    }
}
