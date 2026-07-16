using Microsoft.EntityFrameworkCore;
using Projections.Inline.Domain;
using Nuvora.Nexus.Relay.EventStore;

namespace Projections.Inline.ReadModels;

/// <summary>One flat, query-optimised row per order — its current status and amount.</summary>
public class OrderSummaryReadModel
{
    public Guid OrderId { get; set; }
    public string Customer { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// An INLINE projection: implementing <see cref="IInlineEventProjection"/> and registering it in DI makes
/// the transaction executor apply each just-appended event to it <em>inside the command's transaction</em>,
/// after the append and before commit. Writes are staged on the SAME scoped <c>DbContext</c> the command's
/// unit of work uses (exposed via <see cref="IRelayDbContextAccessor"/>), so the read-model row commits
/// atomically with the events — a SELECT immediately after the command sees it (read-your-writes), with no
/// projection host and no catch-up lag.
///
/// Contrast with <c>IProjection</c> (article 007), which the async host catches up <em>after</em> commit.
/// Like every projection it must stage only — never call <c>SaveChanges</c> — and be idempotent, because a
/// retried command can re-present an event. Note the event's global <c>Position</c> is still 0 here (it is
/// allocated at commit); inline projections key off domain data, not position.
/// </summary>
public sealed class OrderSummaryInlineProjection : IInlineEventProjection
{
    private readonly DbContext _context;
    private readonly IEventSerializer _serializer;

    public OrderSummaryInlineProjection(IRelayDbContextAccessor accessor, IEventSerializer serializer)
    {
        _context = (DbContext)accessor.DbContext;
        _serializer = serializer;
    }

    public bool Handles(string eventType)
        => eventType == typeof(OrderPlaced).FullName
        || eventType == typeof(OrderCancelled).FullName;

    public async Task ApplyAsync(EventData @event, CancellationToken cancellationToken = default)
    {
        var orders = _context.Set<OrderSummaryReadModel>();
        switch (_serializer.DeserializeEvent(@event))
        {
            case OrderPlaced e:
                // Idempotent insert: a retried command could re-present this event in a new transaction.
                if (await orders.FindAsync([e.OrderId], cancellationToken) is null)
                {
                    orders.Add(new OrderSummaryReadModel
                    {
                        OrderId = e.OrderId,
                        Customer = e.Customer,
                        Amount = e.Amount,
                        Status = "Placed",
                    });
                }
                break;

            case OrderCancelled e:
                {
                    // FindAsync resolves the change tracker first, so it sees a row staged earlier in the
                    // same transaction (e.g. OrderPlaced from this very command).
                    var row = await orders.FindAsync([e.OrderId], cancellationToken);
                    if (row is not null) row.Status = "Cancelled";
                }
                break;
        }
    }
}
