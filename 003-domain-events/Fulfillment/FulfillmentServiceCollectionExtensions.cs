using Fulfillment.Tickets;
using Microsoft.Extensions.DependencyInjection;
using Nuvora.Nexus.Relay;
using Nuvora.Nexus.Relay.Cache.InMemory.DependencyInjection;

namespace Fulfillment;

public static class FulfillmentServiceCollectionExtensions
{
    /// <summary>
    /// Registers the in-memory stores and calls <c>AddRelay</c>, which scans this assembly for command
    /// handlers, query handlers and — new in this article — <c>IDomainEventHandler&lt;T&gt;</c>
    /// implementations, and wires up <c>ICommandBus</c>, <c>IQueryBus</c> and <c>IDomainEventBus</c>.
    /// </summary>
    public static IServiceCollection AddFulfillment(this IServiceCollection services)
    {
        services.AddSingleton<TicketStore>();
        services.AddSingleton<TicketReadModelStore>();
        services.AddSingleton<AlertLog>();

        services.AddRelay(typeof(FulfillmentServiceCollectionExtensions).Assembly);

        // The query pipeline's QueryCachingBehavior is constructed by DI for every query, so an
        // ICachingStrategy must be registered even when nothing is [Cacheable]. See article 009.
        services.AddRelayInMemoryCache();

        return services;
    }
}
