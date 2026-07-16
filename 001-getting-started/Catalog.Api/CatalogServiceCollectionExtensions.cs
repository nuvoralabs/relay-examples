using Catalog.Api.Catalog;
using Nuvora.Nexus.Relay;
using Nuvora.Nexus.Relay.Cache.InMemory.DependencyInjection;
using Nuvora.Nexus.Relay.Web;

namespace Catalog.Api;

/// <summary>
/// Registers everything the Catalog API needs. Factored into an extension so the runnable host
/// (Program.cs) and the in-process test host share one source of truth for service registration.
/// </summary>
public static class CatalogServiceCollectionExtensions
{
    public static IServiceCollection AddCatalogServices(this IServiceCollection services)
    {
        // Routing is required by MapRelayEndpoints (the in-process test host adds it explicitly too).
        services.AddRouting();

        // The in-memory "repository". A real service would register a DbContext + repositories here.
        services.AddSingleton<ProductCatalog>();

        // The heart of Relay: scan this assembly for commands, queries, handlers, validators and
        // behaviors and wire up ICommandBus/IQueryBus. Handlers are NEVER registered by hand.
        services.AddRelay(typeof(CatalogServiceCollectionExtensions).Assembly);

        // Register a caching strategy. The query pipeline includes QueryCachingBehavior (it only
        // *caches* [Cacheable] queries), but DI still constructs it for every query — so an app that
        // runs any query needs an ICachingStrategy registered or query dispatch fails. In-memory is
        // the simplest; article 009 uses the same call to actually cache a [Cacheable] query.
        services.AddRelayInMemoryCache();

        // Map thrown exceptions (validation, not-found, …) to RFC 7807 ProblemDetails responses.
        // IncludeExceptionDetails surfaces messages for safe exceptions — handy in development.
        services.AddRelayExceptionHandling(o => o.IncludeExceptionDetails = true);

        return services;
    }
}
