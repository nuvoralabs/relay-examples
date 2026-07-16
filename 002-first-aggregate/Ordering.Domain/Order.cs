using Nuvora.Nexus.Relay.Core.Domain;

namespace Ordering.Domain;

public enum OrderStatus
{
    Draft = 0,
    Confirmed = 1,
    Cancelled = 2,
}

/// <summary>
/// The <c>Order</c> aggregate root — the consistency boundary for a customer's order. Every change
/// goes through a command method that (1) checks invariants with <see cref="Guard"/>, then (2) raises
/// a domain event. Because <see cref="ApplyEventsOnRaise"/> is <c>true</c>, state is mutated <em>only</em>
/// in <see cref="ApplyEvent"/>, which runs both when an event is first raised and when history is
/// replayed. That single mutation path is what makes the aggregate provably replay-safe (see
/// <see cref="FromHistory"/> and the tests), and it is exactly what article 005 builds on to event-source
/// this same class without changing a line of domain logic.
/// </summary>
[AggregateType("ordering.order")]
public sealed class Order : AggregateRoot<Guid>
{
    private readonly List<OrderLine> _lines = new();

    public string CustomerId { get; private set; } = string.Empty;
    public string Currency { get; private set; } = string.Empty;
    public OrderStatus Status { get; private set; } = OrderStatus.Draft;

    public IReadOnlyList<OrderLine> Lines => _lines;

    /// <summary>The order's running total, summed across all lines in the order currency.</summary>
    public Money Total => _lines.Aggregate(Money.Zero(Currency), (running, line) => running.Add(line.LineTotal));

    // Apply is the only place state changes — for live execution AND replay.
    protected override bool ApplyEventsOnRaise => true;

    // Required for rehydration (FromHistory / an event-sourced repository). Kept private so callers
    // must go through the Place factory for a brand-new order.
    private Order()
    {
    }

    // ── Commands ───────────────────────────────────────────────────────────────────────────────────

    /// <summary>Start a new, empty draft order for a customer in a given currency.</summary>
    public static Order Place(Guid id, string customerId, string currency)
    {
        Guard.AgainstEmptyGuid(id, nameof(id));
        Guard.AgainstNullOrWhiteSpace(customerId, nameof(customerId));
        Guard.Against(
            string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3,
            "Currency must be a 3-letter ISO code.");

        var order = new Order();
        order.RaiseEvent(new OrderPlaced(id, customerId.Trim(), currency.Trim().ToUpperInvariant()));
        return order;
    }

    /// <summary>Add a line to a draft order. Quantities and currency are validated up front.</summary>
    public void AddLine(string sku, int quantity, Money unitPrice)
    {
        Guard.Against(Status != OrderStatus.Draft, "Lines can only be added while the order is a draft.");
        Guard.AgainstNull(unitPrice, nameof(unitPrice));
        Guard.AgainstNegativeOrZero(quantity, nameof(quantity));

        var parsedSku = new Sku(sku);
        Guard.Against(_lines.Any(l => l.Sku == parsedSku), $"SKU '{parsedSku}' is already on the order.");
        Guard.Against(
            unitPrice.Currency != Currency,
            $"Line currency '{unitPrice.Currency}' must match the order currency '{Currency}'.");

        RaiseEvent(new OrderLineAdded(
            Id, Guid.NewGuid(), parsedSku.Value, quantity, unitPrice.Amount, unitPrice.Currency));
    }

    /// <summary>Remove a previously added line from a draft order.</summary>
    public void RemoveLine(Guid lineId)
    {
        Guard.Against(Status != OrderStatus.Draft, "Lines can only be removed while the order is a draft.");
        Guard.Against(_lines.All(l => l.Id != lineId), "No such line on the order.");

        RaiseEvent(new OrderLineRemoved(Id, lineId));
    }

    /// <summary>Confirm the order. A confirmed order is immutable and must have at least one line.</summary>
    public void Confirm()
    {
        Guard.Against(Status != OrderStatus.Draft, "Only a draft order can be confirmed.");
        Guard.Against(_lines.Count == 0, "Cannot confirm an order with no lines.");

        RaiseEvent(new OrderConfirmed(Id));
    }

    /// <summary>Cancel the order, recording why.</summary>
    public void Cancel(string reason)
    {
        Guard.AgainstNullOrWhiteSpace(reason, nameof(reason));
        Guard.Against(Status == OrderStatus.Cancelled, "The order is already cancelled.");

        RaiseEvent(new OrderCancelled(Id, reason.Trim()));
    }

    // ── Rehydration ────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Rebuild an order purely from its event history — exactly what an event-sourced repository does.
    /// Because state only ever changes in <see cref="ApplyEvent"/>, the rebuilt order is identical to
    /// the one that produced the events.
    /// </summary>
    public static Order FromHistory(IEnumerable<IDomainEvent> history)
    {
        var order = new Order();
        order.LoadFromHistory(history);
        return order;
    }

    // ── State mutation (the only place fields change) ────────────────────────────────────────────────

    protected override void ApplyEvent(IDomainEvent domainEvent)
    {
        switch (domainEvent)
        {
            case OrderPlaced e:
                SetId(e.OrderId);
                CustomerId = e.CustomerId;
                Currency = e.Currency;
                Status = OrderStatus.Draft;
                break;

            case OrderLineAdded e:
                _lines.Add(new OrderLine(
                    e.LineId, new Sku(e.Sku), e.Quantity, new Money(e.UnitPriceAmount, e.Currency)));
                break;

            case OrderLineRemoved e:
                _lines.RemoveAll(l => l.Id == e.LineId);
                break;

            case OrderConfirmed:
                Status = OrderStatus.Confirmed;
                break;

            case OrderCancelled:
                Status = OrderStatus.Cancelled;
                break;
        }
    }
}
