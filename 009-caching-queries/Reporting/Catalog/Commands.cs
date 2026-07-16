using Nuvora.Nexus.Relay.Cache;
using Nuvora.Nexus.Relay.Core.Application.Commands;

namespace Reporting.Catalog;

/// <summary>
/// Records a sale and then invalidates the cached stats, so the next read recomputes. A command that
/// mutates the data a cacheable query reads is responsible for invalidating that query — caching without
/// invalidation serves stale data.
/// </summary>
[SkipTransaction]
public sealed record RecordSaleCommand(decimal Amount) : ICommand;

public sealed class RecordSaleCommandHandler(CatalogStore store, IQueryCacheInvalidator cacheInvalidator)
    : ICommandHandler<RecordSaleCommand>
{
    public async Task Handle(RecordSaleCommand command, CancellationToken cancellationToken)
    {
        store.RecordSale(command.Amount);

        // Evict every cached result of GetCatalogStatsQuery (regardless of its parameters).
        await cacheInvalidator.InvalidateQueryAsync<GetCatalogStatsQuery>(cancellationToken);
    }
}
