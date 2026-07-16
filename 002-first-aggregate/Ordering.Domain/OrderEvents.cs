using Nuvora.Nexus.Relay.Core.Domain;

namespace Ordering.Domain;

// Domain events are immutable records describing something that already happened, in the past tense.
// They carry primitives (not value objects) so they serialise cleanly — important later when these
// same events become the source of truth in an event store (article 005). Each event derives from
// DomainEvent and points AggregateId at the order it belongs to.

public sealed record OrderPlaced(Guid OrderId, string CustomerId, string Currency) : DomainEvent
{
    public override Guid AggregateId => OrderId;
}

public sealed record OrderLineAdded(
    Guid OrderId,
    Guid LineId,
    string Sku,
    int Quantity,
    decimal UnitPriceAmount,
    string Currency) : DomainEvent
{
    public override Guid AggregateId => OrderId;
}

public sealed record OrderLineRemoved(Guid OrderId, Guid LineId) : DomainEvent
{
    public override Guid AggregateId => OrderId;
}

public sealed record OrderConfirmed(Guid OrderId) : DomainEvent
{
    public override Guid AggregateId => OrderId;
}

public sealed record OrderCancelled(Guid OrderId, string Reason) : DomainEvent
{
    public override Guid AggregateId => OrderId;
}
