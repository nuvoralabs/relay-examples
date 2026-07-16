using Nuvora.Nexus.Relay.Core.Domain;

namespace Projections.Inline.Domain;

// Events for the SHIPMENT stream — a DIFFERENT aggregate from Order. Each shipment event carries the
// OrderId it fulfils, so a multi-stream projection can fold order + shipment events into one read model
// keyed by order, even though they live in separate streams.

public sealed record ShipmentDispatched(Guid ShipmentId, Guid OrderId, string Carrier) : DomainEvent
{
    public override Guid AggregateId => ShipmentId;
}

public sealed record ShipmentDelivered(Guid ShipmentId, Guid OrderId) : DomainEvent
{
    public override Guid AggregateId => ShipmentId;
}

/// <summary>
/// An event-sourced shipment. Its own stream (keyed by <see cref="AggregateRoot{TId}.Id"/> =
/// ShipmentId), separate from the order's. The events reference the order so the fulfillment read model
/// can join the two streams.
/// </summary>
[AggregateType("shop.shipment")]
public sealed class Shipment : AggregateRoot<Guid>
{
    public Guid OrderId { get; private set; }
    public string Carrier { get; private set; } = string.Empty;
    public bool Delivered { get; private set; }

    protected override bool ApplyEventsOnRaise => true;

    private Shipment() { }

    public static Shipment Dispatch(Guid id, Guid orderId, string carrier)
    {
        Guard.AgainstEmptyGuid(id, nameof(id));
        Guard.AgainstEmptyGuid(orderId, nameof(orderId));
        Guard.AgainstNullOrWhiteSpace(carrier, nameof(carrier));

        var shipment = new Shipment();
        shipment.RaiseEvent(new ShipmentDispatched(id, orderId, carrier.Trim()));
        return shipment;
    }

    public void MarkDelivered()
    {
        Guard.Against(Delivered, "Shipment is already delivered.");
        RaiseEvent(new ShipmentDelivered(Id, OrderId));
    }

    protected override void ApplyEvent(IDomainEvent domainEvent)
    {
        switch (domainEvent)
        {
            case ShipmentDispatched e:
                SetId(e.ShipmentId);
                OrderId = e.OrderId;
                Carrier = e.Carrier;
                break;
            case ShipmentDelivered:
                Delivered = true;
                break;
        }
    }
}
