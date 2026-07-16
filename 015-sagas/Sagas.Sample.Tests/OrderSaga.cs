using Nuvora.Nexus.Relay.Core.Application.Events;
using Nuvora.Nexus.Relay.Sagas;
using Nuvora.Nexus.Relay.Sagas.Configuration;

namespace Sagas.Sample;

// Messages the saga reacts to (integration events) and the timeout it schedules.
public sealed record OrderPlaced : IntegrationEvent { public Guid OrderId { get; init; } }
public sealed record PaymentReceived : IntegrationEvent { public Guid OrderId { get; init; } }
public sealed class OrderExpired { public Guid OrderId { get; set; } }

/// <summary>The saga's persisted state — serialised between messages.</summary>
public sealed class OrderState : ISagaState
{
    public Guid OrderId { get; set; }
    public bool Paid { get; set; }
}

/// <summary>
/// An imperative process manager. <c>StartedBy</c> begins a new instance when an <see cref="OrderPlaced"/>
/// arrives (correlated by order id); <c>Handle</c> routes later messages to the existing instance;
/// <c>OnTimeout</c> handles a scheduled timeout. The saga reacts by mutating <see cref="State"/>, requesting
/// or cancelling timeouts, and completing — the coordinator persists the state and flushes the scheduled
/// timeouts atomically.
/// </summary>
public sealed class OrderSaga : Saga<OrderState>
{
    protected override void Configure(ISagaConfigurator<OrderState> configurator) => configurator
        .StartedBy<OrderPlaced>(e => e.OrderId, OnPlaced)
        .Handle<PaymentReceived>(e => e.OrderId, OnPaid)
        .OnTimeout<OrderExpired>(OnExpired);

    private Task OnPlaced(OrderPlaced e, CancellationToken cancellationToken)
    {
        State.OrderId = e.OrderId;
        // If payment doesn't arrive in time, the saga will receive OrderExpired.
        RequestTimeout(TimeSpan.FromHours(1), new OrderExpired { OrderId = e.OrderId });
        return Task.CompletedTask;
    }

    private Task OnPaid(PaymentReceived e, CancellationToken cancellationToken)
    {
        State.Paid = true;
        CancelTimeouts(); // payment arrived — the expiry no longer applies
        Complete();
        return Task.CompletedTask;
    }

    private Task OnExpired(OrderExpired t, CancellationToken cancellationToken)
    {
        Complete(); // give up; a real saga would publish OrderCancelled here
        return Task.CompletedTask;
    }
}
