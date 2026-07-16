using Microsoft.EntityFrameworkCore;
using Projections.Inline.Domain;
using Nuvora.Nexus.Relay.EventStore;
using Nuvora.Nexus.Relay.Projections;

namespace Projections.Inline.ReadModels;

/// <summary>
/// A denormalised read model that folds TWO streams — the order's and its shipment's — into one row:
/// who ordered, for how much, and where the shipment is.
/// </summary>
public class FulfillmentReadModel
{
    public Guid OrderId { get; set; }
    public string Customer { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Carrier { get; set; }
    public string FulfillmentStatus { get; set; } = string.Empty;
}

/// <summary>
/// A <see cref="MultiStreamProjection"/>: instead of switching on the raw <see cref="EventData"/>, it
/// registers strongly-typed <c>On&lt;TEvent&gt;</c> handlers in <see cref="Configure"/>. The base maps each
/// handled type to its stable persisted name (via the <see cref="IEventTypeRegistry"/>), reports them
/// through <c>Handles</c>, deserializes each delivered event (via the <see cref="IEventSerializer"/>), and
/// routes it to the matching handler.
///
/// Crucially the handled events come from DIFFERENT aggregates/streams — <see cref="OrderPlaced"/> from the
/// order stream, <see cref="ShipmentDispatched"/>/<see cref="ShipmentDelivered"/> from the shipment stream —
/// yet they fold into one <see cref="FulfillmentReadModel"/> keyed by order id. That cross-stream fold is
/// exactly what the base is for. Handlers stage writes on the shared scoped <c>DbContext</c>, like any
/// <see cref="IProjection"/>; the host (article 007) owns the commit.
/// </summary>
public sealed class FulfillmentProjection : MultiStreamProjection
{
    private readonly DbContext _context;

    public FulfillmentProjection(
        IRelayDbContextAccessor accessor,
        IEventSerializer serializer,
        IEventTypeRegistry eventTypeRegistry)
        : base(serializer, eventTypeRegistry)
    {
        _context = (DbContext)accessor.DbContext;
    }

    public override string Name => "fulfillment";

    protected override void Configure()
    {
        // ORDER stream → create/seed the row.
        On<OrderPlaced>(async (e, _, ct) =>
        {
            var rows = _context.Set<FulfillmentReadModel>();
            if (await rows.FindAsync([e.OrderId], ct) is null)
            {
                rows.Add(new FulfillmentReadModel
                {
                    OrderId = e.OrderId,
                    Customer = e.Customer,
                    Amount = e.Amount,
                    FulfillmentStatus = "Ordered",
                });
            }
        });

        // SHIPMENT stream → advance the SAME order row. The event carries OrderId so the cross-stream
        // join is just a FindAsync on the order key.
        On<ShipmentDispatched>(async (e, _, ct) =>
        {
            var row = await _context.Set<FulfillmentReadModel>().FindAsync([e.OrderId], ct);
            if (row is not null)
            {
                row.Carrier = e.Carrier;
                row.FulfillmentStatus = "Dispatched";
            }
        });

        On<ShipmentDelivered>(async (e, _, ct) =>
        {
            var row = await _context.Set<FulfillmentReadModel>().FindAsync([e.OrderId], ct);
            if (row is not null)
            {
                row.FulfillmentStatus = "Delivered";
            }
        });
    }
}
