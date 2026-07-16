using FluentAssertions;
using Nuvora.Nexus.Relay.Core.Coordination;
using Xunit;

namespace Coordination.Fenced;

/// <summary>
/// <see cref="LeaseLeaderElector"/> (an <see cref="IFencedLeaderElector"/>) turns a renewable
/// <see cref="ILeaseStore"/> into leader election: when elected it invokes the work with the current
/// fencing token, renews the lease while the work runs, and a second instance contending for the same
/// role does <em>not</em> run concurrently. Driven over a real <see cref="InMemoryLeaseStore"/> with very
/// short renew/retry intervals and bounded waits, so a regression fails fast — no database.
/// </summary>
public sealed class FencedLeaderElectionTests
{
    private static LeaseLeaderOptions OptionsFor(string ownerId) => new()
    {
        OwnerId = ownerId,
        LeaseTtl = TimeSpan.FromSeconds(5),
        RenewInterval = TimeSpan.FromMilliseconds(20),
        RetryInterval = TimeSpan.FromMilliseconds(20),
    };

    private static async Task<bool> WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(10);
        }

        return condition();
    }

    [Fact]
    public async Task The_elected_leader_runs_the_work_with_a_fencing_token()
    {
        var store = new InMemoryLeaseStore(); // single store, shared as the cluster-wide arbiter
        var elector = new LeaseLeaderElector(store, OptionsFor("node-a"));
        using var cts = new CancellationTokenSource();

        long? observedToken = null;
        var invocations = 0;
        var run = elector.RunAsync("reports", async (fencingToken, leaderCt) =>
        {
            Interlocked.Increment(ref invocations);
            observedToken = fencingToken;
            try { await Task.Delay(Timeout.Infinite, leaderCt); }
            catch (OperationCanceledException) { /* leadership ended on shutdown */ }
        }, cts.Token);

        (await WaitUntil(() => observedToken is not null, TimeSpan.FromSeconds(2)))
            .Should().BeTrue("the only contender is elected and runs the work");

        cts.Cancel();
        await run.WaitAsync(TimeSpan.FromSeconds(5));

        observedToken.Should().Be(1, "the first holder of a fresh lease gets fencing token 1");
        invocations.Should().Be(1, "leadership is held until cancelled — the work is invoked once");
    }

    [Fact]
    public async Task A_second_instance_does_not_run_concurrently_with_the_leader()
    {
        var store = new InMemoryLeaseStore(); // both electors contend over the same lease store
        using var cts = new CancellationTokenSource();

        var leaderARunning = false;
        var leaderBRan = false;

        var runA = new LeaseLeaderElector(store, OptionsFor("node-a")).RunAsync("reports",
            async (_, leaderCt) =>
            {
                leaderARunning = true;
                try { await Task.Delay(Timeout.Infinite, leaderCt); }
                catch (OperationCanceledException) { }
            }, cts.Token);

        (await WaitUntil(() => leaderARunning, TimeSpan.FromSeconds(2))).Should().BeTrue();

        var runB = new LeaseLeaderElector(store, OptionsFor("node-b")).RunAsync("reports",
            (_, _) => { leaderBRan = true; return Task.CompletedTask; }, cts.Token);

        // Give node-b ample time to contend and (wrongly) start. It must keep losing while node-a renews.
        await Task.Delay(300);
        leaderBRan.Should().BeFalse("node-a holds and renews the lease, so node-b never wins it");

        cts.Cancel();
        await Task.WhenAll(runA, runB).WaitAsync(TimeSpan.FromSeconds(5));
    }
}
