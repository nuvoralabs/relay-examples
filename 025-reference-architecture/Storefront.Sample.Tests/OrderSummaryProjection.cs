using Microsoft.EntityFrameworkCore;
using Nuvora.Nexus.Relay.EventStore;
using Nuvora.Nexus.Relay.Projections;

namespace Storefront.Sample;

/// <summary>The query-optimised read model: one flat row per order.</summary>
public class OrderSummary
{
    public Guid OrderId { get; set; }
    public string Customer { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Keeps <see cref="OrderSummary"/> in sync with the order event stream. The projection host applies a
/// batch and commits the read-model write together with the checkpoint advance, so the read model can
/// never be ahead of the events it reflects. Lookups use <c>FindAsync</c> (change-tracker aware) so a row
/// inserted earlier in the same batch is visible to a later event in that batch.
/// </summary>
public sealed class OrderSummaryProjection(IRelayDbContextAccessor accessor, IEventSerializer serializer) : IProjection
{
    private readonly DbContext _context = (DbContext)accessor.DbContext;

    public string Name => "order-summary";

    public bool Handles(string eventType)
        => eventType == typeof(OrderPlaced).FullName
        || eventType == typeof(OrderPaid).FullName;

    public async Task ProjectAsync(EventData @event, CancellationToken cancellationToken = default)
    {
        var orders = _context.Set<OrderSummary>();
        switch (serializer.DeserializeEvent(@event))
        {
            case OrderPlaced e:
                if (await orders.FindAsync([e.OrderId], cancellationToken) is null)
                {
                    orders.Add(new OrderSummary { OrderId = e.OrderId, Customer = e.Customer, Total = e.Total, Status = "Placed" });
                }
                break;

            case OrderPaid e:
                {
                    var row = await orders.FindAsync([e.OrderId], cancellationToken);
                    if (row is not null) row.Status = "Paid";
                }
                break;
        }
    }
}
