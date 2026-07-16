using Microsoft.EntityFrameworkCore;
using Nuvora.Nexus.Relay.Core.Application.Queries;

namespace Storefront.Sample;

public sealed record GetOrderSummaryQuery(Guid OrderId) : IQuery<OrderSummary?>;

/// <summary>Reads the projected read model — never the event stream. Returns null (→ not found) when the
/// projection has not yet caught up or the order does not exist.</summary>
public sealed class GetOrderSummaryQueryHandler(StorefrontDbContext db) : IQueryHandler<GetOrderSummaryQuery, OrderSummary?>
{
    public Task<OrderSummary?> Handle(GetOrderSummaryQuery query, CancellationToken cancellationToken)
        => db.OrderSummaries.AsNoTracking().FirstOrDefaultAsync(r => r.OrderId == query.OrderId, cancellationToken);
}
