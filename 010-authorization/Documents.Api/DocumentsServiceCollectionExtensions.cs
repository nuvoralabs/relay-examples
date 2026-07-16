using Documents.Api.Documents;
using Nuvora.Nexus.Relay;
using Nuvora.Nexus.Relay.Auth;
using Nuvora.Nexus.Relay.Cache.InMemory.DependencyInjection;
using Nuvora.Nexus.Relay.Web;

namespace Documents.Api;

public static class DocumentsServiceCollectionExtensions
{
    public static IServiceCollection AddDocumentsApi(this IServiceCollection services)
    {
        services.AddRouting();
        services.AddSingleton<DocumentStore>();

        services.AddRelay(typeof(DocumentsServiceCollectionExtensions).Assembly);

        // DI constructs QueryCachingBehavior for every query, so an ICachingStrategy is required even
        // though nothing here is [Cacheable]; without it the document queries fail to dispatch (500).
        services.AddRelayInMemoryCache();

        // Wires the authorization pipeline behaviors. With this registered, a message carrying a
        // [Require*] attribute is enforced; an UNattributed message requires an authenticated user
        // (fail-closed). Omitting AddRelayAuth while [Require*] attributes exist fails startup.
        services.AddRelayAuth();

        // Maps the UnauthorizedException (401) / ForbiddenException (403) the behaviors throw.
        services.AddRelayExceptionHandling(o => o.IncludeExceptionDetails = true);

        return services;
    }
}
