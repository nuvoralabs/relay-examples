using FluentAssertions;
using Nuvora.Nexus.Relay.Scheduling.Domain;
using Nuvora.Nexus.Relay.Scheduling.Serialization;
using Xunit;

namespace Sagas.RoutingSlip;

public sealed class RoutingSlipTests
{
    private readonly SystemTextJsonScheduledMessageSerializer _serializer = new();

    private string[] ScheduledCompensations(SagaHarness harness) => harness.Scheduler.Scheduled
        .Where(m => m.Kind == ScheduledDeliveryKind.Command)
        .Select(m => ((CancelStep)_serializer.Deserialize(m.MessageType, m.Payload)).Step)
        .ToArray();

    [Fact]
    public async Task Forward_legs_record_compensations_without_issuing_them()
    {
        var harness = SagaHarness.For<BookingSaga, BookingState>();
        var tripId = Guid.NewGuid();

        await harness.Coordinator.DeliverAsync(typeof(BookingSaga), new TripStarted { TripId = tripId }, default);
        await harness.Coordinator.DeliverAsync(typeof(BookingSaga), new HotelBooked { TripId = tripId }, default);
        await harness.Coordinator.DeliverAsync(typeof(BookingSaga), new CarBooked { TripId = tripId }, default);

        harness.Repo.Single().ItineraryPosition.Should().Be(0, "nothing has been compensated yet");
        ScheduledCompensations(harness).Should().BeEmpty("forward legs only RECORD compensations");
    }

    [Fact]
    public async Task A_failure_replays_compensations_in_reverse_order()
    {
        var harness = SagaHarness.For<BookingSaga, BookingState>();
        var tripId = Guid.NewGuid();
        await harness.Coordinator.DeliverAsync(typeof(BookingSaga), new TripStarted { TripId = tripId }, default);
        await harness.Coordinator.DeliverAsync(typeof(BookingSaga), new HotelBooked { TripId = tripId }, default);
        await harness.Coordinator.DeliverAsync(typeof(BookingSaga), new CarBooked { TripId = tripId }, default);

        await harness.Coordinator.DeliverAsync(typeof(BookingSaga), new TripFailed { TripId = tripId }, default);

        ScheduledCompensations(harness).Should().Equal("car", "hotel", "flight"); // LIFO
        harness.Repo.Single().ItineraryPosition.Should().Be(3, "all three legs were compensated");
    }

    [Fact]
    public async Task A_redelivered_failure_does_not_re_issue_compensations()
    {
        var harness = SagaHarness.For<BookingSaga, BookingState>();
        var tripId = Guid.NewGuid();
        await harness.Coordinator.DeliverAsync(typeof(BookingSaga), new TripStarted { TripId = tripId }, default);
        await harness.Coordinator.DeliverAsync(typeof(BookingSaga), new HotelBooked { TripId = tripId }, default);
        await harness.Coordinator.DeliverAsync(typeof(BookingSaga), new TripFailed { TripId = tripId }, default);
        var afterFirst = ScheduledCompensations(harness).Length;

        // A duplicate failure event must compensate nothing new — the committed itinerary position guards it.
        await harness.Coordinator.DeliverAsync(typeof(BookingSaga), new TripFailed { TripId = tripId }, default);

        ScheduledCompensations(harness).Length.Should().Be(afterFirst, "a repeated rollback is idempotent");
        afterFirst.Should().Be(2, "two legs (flight, hotel) were booked before the failure");
    }
}
