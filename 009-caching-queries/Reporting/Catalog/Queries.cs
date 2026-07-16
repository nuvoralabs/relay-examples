using Nuvora.Nexus.Relay.Bus.Behaviors;
using Nuvora.Nexus.Relay.Core.Application.Queries;

namespace Reporting.Catalog;

public sealed record CatalogStats(int SalesCount, decimal TotalRevenue);

/// <summary>
/// A cacheable query. The <c>[Cacheable]</c> attribute makes the QueryCachingBehavior store the result
/// keyed by the query type + its (serialized) properties, with stampede protection, for the given
/// duration. Identical queries within that window are served from cache without running the handler.
/// </summary>
[Cacheable(DurationSeconds = 60)]
public sealed record GetCatalogStatsQuery : IQuery<CatalogStats>;

public sealed class GetCatalogStatsQueryHandler(CatalogStore store, QueryExecutionCounter counter)
    : IQueryHandler<GetCatalogStatsQuery, CatalogStats>
{
    public Task<CatalogStats> Handle(GetCatalogStatsQuery query, CancellationToken cancellationToken)
    {
        counter.Increment(); // only reached on a cache MISS
        var (salesCount, totalRevenue) = store.Snapshot();
        return Task.FromResult(new CatalogStats(salesCount, totalRevenue));
    }
}
