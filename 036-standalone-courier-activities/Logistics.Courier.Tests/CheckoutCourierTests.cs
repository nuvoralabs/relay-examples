using FluentAssertions;
using Nuvora.Nexus.Relay.Sagas.Courier;
using Xunit;

namespace Logistics.Courier;

/// <summary>
/// A standalone Courier: <see cref="CourierExecutor"/> runs an ordered itinerary of
/// <see cref="ICourierActivity"/> steps. On success every step runs forward and nothing is
/// compensated; on a failure at step N, the steps that already completed are undone in reverse
/// (LIFO) order, the failing step and the not-yet-run steps are left untouched, and the
/// <see cref="CourierResult"/> reports the failure. No saga, no state machine, no database —
/// just an in-process sequence with compensation.
///
/// The activities below model a checkout flow (reserve stock, charge the card, book a courier),
/// each recording its execute/compensate calls into a shared log so the order is observable.
/// </summary>
public sealed class CheckoutCourierTests
{
    /// <summary>
    /// A test <see cref="ICourierActivity"/> that records each forward and compensating call into a
    /// shared, ordered log. Optionally fails its forward action to trigger compensation.
    /// </summary>
    private sealed class RecordingActivity : ICourierActivity
    {
        private readonly List<string> _log;
        private readonly string _name;
        private readonly bool _failExecute;

        public RecordingActivity(List<string> log, string name, bool failExecute = false)
        {
            _log = log;
            _name = name;
            _failExecute = failExecute;
        }

        public Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            _log.Add("exec:" + _name);
            if (_failExecute)
            {
                throw new InvalidOperationException("execute failed: " + _name);
            }

            return Task.CompletedTask;
        }

        public Task CompensateAsync(CancellationToken cancellationToken = default)
        {
            _log.Add("comp:" + _name);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task All_steps_succeed_then_every_activity_runs_forward_and_nothing_is_compensated()
    {
        var log = new List<string>();
        var courier = new CourierExecutor();

        var result = await courier.ExecuteAsync(new ICourierActivity[]
        {
            new RecordingActivity(log, "ReserveStock"),
            new RecordingActivity(log, "ChargeCard"),
            new RecordingActivity(log, "BookCourier"),
        });

        result.Succeeded.Should().BeTrue();
        result.Failure.Should().BeNull();
        result.CompensationErrors.Should().BeEmpty();

        // Every activity ran forward, in order; no "comp:" entries were recorded.
        log.Should().Equal("exec:ReserveStock", "exec:ChargeCard", "exec:BookCourier");
    }

    [Fact]
    public async Task A_failure_at_step_N_compensates_the_completed_steps_in_reverse_LIFO_order_and_reports_failure()
    {
        var log = new List<string>();
        var courier = new CourierExecutor();

        var result = await courier.ExecuteAsync(new ICourierActivity[]
        {
            new RecordingActivity(log, "ReserveStock"),
            new RecordingActivity(log, "ChargeCard"),
            new RecordingActivity(log, "BookCourier", failExecute: true),
        });

        // The itinerary did not complete; the courier reports the failure that triggered the rollback.
        result.Succeeded.Should().BeFalse();
        result.Failure.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Be("execute failed: BookCourier");
        result.CompensationErrors.Should().BeEmpty();

        // ReserveStock + ChargeCard committed; BookCourier failed; the two committed steps are
        // undone in REVERSE (LIFO) order — ChargeCard first, then ReserveStock.
        log.Should().Equal(
            "exec:ReserveStock",
            "exec:ChargeCard",
            "exec:BookCourier",
            "comp:ChargeCard",
            "comp:ReserveStock");
    }

    [Fact]
    public async Task The_failing_step_and_not_yet_run_steps_are_never_compensated()
    {
        var log = new List<string>();
        var courier = new CourierExecutor();

        // ChargeCard fails on its forward action; BookCourier never runs at all.
        var result = await courier.ExecuteAsync(new ICourierActivity[]
        {
            new RecordingActivity(log, "ReserveStock"),
            new RecordingActivity(log, "ChargeCard", failExecute: true),
            new RecordingActivity(log, "BookCourier"),
        });

        result.Succeeded.Should().BeFalse();

        // Only ReserveStock committed, so only ReserveStock is compensated.
        // The failing step (ChargeCard) is never compensated — its forward action did not commit.
        // The not-yet-run step (BookCourier) is never executed and never compensated.
        log.Should().Equal("exec:ReserveStock", "exec:ChargeCard", "comp:ReserveStock");
        log.Should().NotContain("exec:BookCourier");
        log.Should().NotContain("comp:BookCourier");
        log.Should().NotContain("comp:ChargeCard");
    }
}
