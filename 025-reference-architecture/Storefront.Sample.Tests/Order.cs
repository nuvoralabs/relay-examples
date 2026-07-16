using Nuvora.Nexus.Relay.Core.Application.Events;
using Nuvora.Nexus.Relay.Core.Domain;

namespace Storefront.Sample;

// Domain events — the source of truth, appended to the event store.
public sealed record OrderPlaced(Guid OrderId, string Customer, decimal Total) : DomainEvent
{
    public override Guid AggregateId => OrderId;
}

public sealed record OrderPaid(Guid OrderId) : DomainEvent
{
    public override Guid AggregateId => OrderId;
}

// Integration event — a fact announced to OTHER services, staged on the outbox in the same transaction.
public sealed record OrderPlacedNotification : IntegrationEvent
{
    public Guid OrderId { get; init; }
}

/// <summary>
/// The event-sourced order aggregate. State is derived by replaying its events; behaviour raises new
/// ones. The repository persists uncommitted events inside the command's transaction.
/// </summary>
[AggregateType("storefront.order")]
public sealed class Order : AggregateRoot<Guid>
{
    public string Customer { get; private set; } = string.Empty;
    public decimal Total { get; private set; }
    public bool Paid { get; private set; }

    protected override bool ApplyEventsOnRaise => true;

    private Order() { }

    public static Order Place(Guid id, string customer, decimal total)
    {
        Guard.AgainstEmptyGuid(id, nameof(id));
        Guard.AgainstNullOrWhiteSpace(customer, nameof(customer));
        Guard.AgainstNegativeOrZero(total, nameof(total));

        var order = new Order();
        order.RaiseEvent(new OrderPlaced(id, customer.Trim(), total));
        return order;
    }

    public void MarkPaid()
    {
        if (Paid)
        {
            return; // idempotent: paying an already-paid order raises nothing
        }

        RaiseEvent(new OrderPaid(Id));
    }

    protected override void ApplyEvent(IDomainEvent domainEvent)
    {
        switch (domainEvent)
        {
            case OrderPlaced e:
                SetId(e.OrderId);
                Customer = e.Customer;
                Total = e.Total;
                break;
            case OrderPaid:
                Paid = true;
                break;
        }
    }
}
