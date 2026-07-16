using Nuvora.Nexus.Relay.Core.Application.Commands;
using Nuvora.Nexus.Relay.Core.Application.Events;
using Nuvora.Nexus.Relay.Sagas;
using Nuvora.Nexus.Relay.Sagas.Configuration;

namespace Sagas.RoutingSlip;

// A trip-booking workflow that books a flight, a hotel, and a car across services. If any step fails,
// the already-completed steps must be undone — a distributed transaction without 2PC, via compensation.

public sealed record TripStarted : IntegrationEvent { public Guid TripId { get; init; } }
public sealed record HotelBooked : IntegrationEvent { public Guid TripId { get; init; } }
public sealed record CarBooked : IntegrationEvent { public Guid TripId { get; init; } }
public sealed record TripFailed : IntegrationEvent { public Guid TripId { get; init; } }

/// <summary>A compensating command — undoes one booked step.</summary>
public sealed record CancelStep : ICommand { public string Step { get; init; } = string.Empty; }

public sealed class BookingState : ISagaState { public Guid TripId { get; set; } }

/// <summary>
/// A routing-slip saga (Courier pattern). Each forward leg <c>RecordCompensation(...)</c>s the command
/// that would undo it onto the saga's persisted itinerary. On failure, <c>Compensate()</c> replays those
/// compensations in REVERSE (LIFO) as reliable, scheduled commands — and because the itinerary position
/// is committed, a redelivered failure compensates nothing new (idempotent rollback).
/// </summary>
public sealed class BookingSaga : Saga<BookingState>
{
    protected override void Configure(ISagaConfigurator<BookingState> configurator) => configurator
        .StartedBy<TripStarted>(e => e.TripId, OnStarted)
        .Handle<HotelBooked>(e => e.TripId, OnHotel)
        .Handle<CarBooked>(e => e.TripId, OnCar)
        .Handle<TripFailed>(e => e.TripId, OnFailed);

    private Task OnStarted(TripStarted e, CancellationToken ct)
    {
        State.TripId = e.TripId;
        RecordCompensation(new CancelStep { Step = "flight" });
        return Task.CompletedTask;
    }

    private Task OnHotel(HotelBooked e, CancellationToken ct)
    {
        RecordCompensation(new CancelStep { Step = "hotel" });
        return Task.CompletedTask;
    }

    private Task OnCar(CarBooked e, CancellationToken ct)
    {
        RecordCompensation(new CancelStep { Step = "car" });
        return Task.CompletedTask;
    }

    private Task OnFailed(TripFailed e, CancellationToken ct)
    {
        Compensate(); // replay recorded compensations in reverse order
        Complete();
        return Task.CompletedTask;
    }
}
