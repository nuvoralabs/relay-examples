using Nuvora.Nexus.Relay.Core.Domain;

namespace Ordering.Metadata.Ordering;

// Domain events — the source of truth for an order. The metadata that gets stamped onto them
// (correlation id, actor, tenant) lives ALONGSIDE each stored event, not inside the payload, so the
// events stay clean business facts while the audit/tracing context rides on the event envelope.

public sealed record OrderPlaced(Guid OrderId, string Sku, int Quantity) : DomainEvent
{
    public override Guid AggregateId => OrderId;
}

public sealed record OrderQuantityChanged(Guid OrderId, int NewQuantity) : DomainEvent
{
    public override Guid AggregateId => OrderId;
}

/// <summary>
/// A minimal event-sourced order — the same apply-only shape from article 002/005. The aggregate knows
/// nothing about metadata: it just raises business events. Stamping correlation/actor/tenant onto those
/// events is a cross-cutting concern handled entirely by the registered <see cref="RequestContextEnricher"/>
/// in the append path, with zero changes to the domain.
/// </summary>
[AggregateType("ordering.order")]
public sealed class Order : AggregateRoot<Guid>
{
    public string Sku { get; private set; } = string.Empty;
    public int Quantity { get; private set; }

    protected override bool ApplyEventsOnRaise => true;

    // Parameterless ctor used by the repository to rehydrate from history.
    private Order() { }

    public static Order Place(Guid id, string sku, int quantity)
    {
        Guard.AgainstEmptyGuid(id, nameof(id));
        Guard.AgainstNullOrWhiteSpace(sku, nameof(sku));
        Guard.AgainstNegativeOrZero(quantity, nameof(quantity));

        var order = new Order();
        order.RaiseEvent(new OrderPlaced(id, sku.Trim(), quantity));
        return order;
    }

    public void ChangeQuantity(int newQuantity)
    {
        Guard.AgainstNegativeOrZero(newQuantity, nameof(newQuantity));
        RaiseEvent(new OrderQuantityChanged(Id, newQuantity));
    }

    protected override void ApplyEvent(IDomainEvent domainEvent)
    {
        switch (domainEvent)
        {
            case OrderPlaced e:
                SetId(e.OrderId);
                Sku = e.Sku;
                Quantity = e.Quantity;
                break;
            case OrderQuantityChanged e:
                Quantity = e.NewQuantity;
                break;
        }
    }
}
