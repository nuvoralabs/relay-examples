using Microsoft.Extensions.DependencyInjection;
using Nuvora.Nexus.Relay;
using Nuvora.Nexus.Relay.Cache.InMemory.DependencyInjection;
using Reporting.Catalog;

namespace Reporting;

public static class ReportingServiceCollectionExtensions
{
    public static IServiceCollection AddReporting(this IServiceCollection services)
    {
        services.AddSingleton<CatalogStore>();
        services.AddSingleton<QueryExecutionCounter>();

        services.AddRelay(typeof(ReportingServiceCollectionExtensions).Assembly);

        // Registers ICachingStrategy (in-memory) and IQueryCacheInvalidator. The QueryCachingBehavior —
        // a built-in behavior that only runs for [Cacheable] queries — uses them.
        services.AddRelayInMemoryCache();

        return services;
    }
}
