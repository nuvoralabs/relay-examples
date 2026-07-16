using Nuvora.Nexus.Relay.Core.Domain;

namespace Projections.Inline.Domain;

// Events for the ORDER stream. They are the source of truth; the read models below are derived.

public sealed record OrderPlaced(Guid OrderId, string Customer, decimal Amount) : DomainEvent
{
    public override Guid AggregateId => OrderId;
}

public sealed record OrderCancelled(Guid OrderId) : DomainEvent
{
    public override Guid AggregateId => OrderId;
}

/// <summary>
/// An event-sourced order. Same apply-only aggregate shape as the earlier samples: every command checks
/// invariants then raises an event, and state changes only in <see cref="ApplyEvent"/>. The transactional
/// pipeline appends the new events to the store on commit — and (article 026) feeds them to any registered
/// inline projection inside that same transaction, so a read model can be read-your-writes consistent.
/// </summary>
[AggregateType("shop.order")]
public sealed class Order : AggregateRoot<Guid>
{
    public string Customer { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public bool Cancelled { get; private set; }

    protected override bool ApplyEventsOnRaise => true;

    private Order() { }

    public static Order Place(Guid id, string customer, decimal amount)
    {
        Guard.AgainstEmptyGuid(id, nameof(id));
        Guard.AgainstNullOrWhiteSpace(customer, nameof(customer));
        Guard.AgainstNegativeOrZero(amount, nameof(amount));

        var order = new Order();
        order.RaiseEvent(new OrderPlaced(id, customer.Trim(), amount));
        return order;
    }

    public void Cancel()
    {
        Guard.Against(Cancelled, "Order is already cancelled.");
        RaiseEvent(new OrderCancelled(Id));
    }

    protected override void ApplyEvent(IDomainEvent domainEvent)
    {
        switch (domainEvent)
        {
            case OrderPlaced e:
                SetId(e.OrderId);
                Customer = e.Customer;
                Amount = e.Amount;
                break;
            case OrderCancelled:
                Cancelled = true;
                break;
        }
    }
}
