using Nuvora.Nexus.Relay.Core.Domain;

namespace Ordering.Domain;

/// <summary>
/// An entity that lives <em>inside</em> the <see cref="Order"/> aggregate. Unlike a value object an
/// entity has identity (<see cref="Entity{TId}.Id"/>): two lines with the same SKU and quantity are
/// still distinct lines. Entities inside an aggregate are only ever created and mutated through the
/// aggregate root, which is why this type has no public command methods of its own.
/// </summary>
public sealed class OrderLine : Entity<Guid>
{
    public Sku Sku { get; }
    public int Quantity { get; }
    public Money UnitPrice { get; }

    public Money LineTotal => UnitPrice.Multiply(Quantity);

    public OrderLine(Guid id, Sku sku, int quantity, Money unitPrice)
        : base(id)
    {
        Guard.AgainstNull(sku, nameof(sku));
        Guard.AgainstNegativeOrZero(quantity, nameof(quantity));
        Guard.AgainstNull(unitPrice, nameof(unitPrice));

        Sku = sku;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }
}
