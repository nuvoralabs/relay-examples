using FluentAssertions;
using Nuvora.Nexus.Relay.Scheduling.Domain;
using Nuvora.Nexus.Relay.Scheduling.Recurring;
using Xunit;

namespace Recurring.Sample;

/// <summary>
/// The cron occurrence planner is pure (no infrastructure): it computes when a recurring schedule should
/// next fire, and — on catch-up — which missed occurrences to enqueue (Skip vs Backfill), in a time zone.
/// </summary>
public sealed class CronTests
{
    private readonly CronRecurringOccurrencePlanner _planner = new();

    private static DateTimeOffset Utc(int y, int mo, int d, int h = 0, int mi = 0)
        => new(y, mo, d, h, mi, 0, TimeSpan.Zero);

    [Fact]
    public void The_first_occurrence_is_the_next_daily_run()
        => _planner.GetFirstOccurrence("0 2 * * *", "UTC", Utc(2026, 1, 1, 3))
            .Should().Be(Utc(2026, 1, 2, 2)); // already past 02:00 today, so tomorrow at 02:00

    [Fact]
    public void Skip_fires_the_due_occurrence_once_and_resumes_in_the_future()
    {
        var schedule = new RecurringSchedule
        {
            Name = "nightly-report",
            CronExpression = "0 2 * * *",
            TimeZoneId = "UTC",
            CatchUpPolicy = RecurringCatchUpPolicy.Skip,
            NextFireAt = Utc(2026, 1, 1, 2),
        };

        var plan = _planner.Plan(schedule, now: Utc(2026, 1, 5, 12));

        plan.Occurrences.Should().ContainSingle().Which.Should().Be(Utc(2026, 1, 1, 2));
        plan.NextFireAt.Should().Be(Utc(2026, 1, 6, 2), "Skip drops the missed runs and resumes from the next future one");
    }

    [Fact]
    public void Backfill_enqueues_every_missed_occurrence_in_order()
    {
        var schedule = new RecurringSchedule
        {
            CronExpression = "0 2 * * *",
            TimeZoneId = "UTC",
            CatchUpPolicy = RecurringCatchUpPolicy.Backfill,
            MaxBackfill = 10,
            NextFireAt = Utc(2026, 1, 1, 2),
        };

        var plan = _planner.Plan(schedule, now: Utc(2026, 1, 4, 12));

        plan.Occurrences.Should().Equal(Utc(2026, 1, 1, 2), Utc(2026, 1, 2, 2), Utc(2026, 1, 3, 2), Utc(2026, 1, 4, 2));
        plan.NextFireAt.Should().Be(Utc(2026, 1, 5, 2));
    }
}
