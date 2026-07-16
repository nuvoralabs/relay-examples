using Nuvora.Nexus.Relay;
using Nuvora.Nexus.Relay.Cache.InMemory.DependencyInjection;
using Nuvora.Nexus.Relay.Web;
using Payments.Api.Payments;

namespace Payments.Api;

public static class PaymentsServiceCollectionExtensions
{
    public static IServiceCollection AddPaymentsApi(this IServiceCollection services)
    {
        services.AddRouting();
        services.AddSingleton<PaymentStore>();
        services.AddRelay(typeof(PaymentsServiceCollectionExtensions).Assembly);

        // Required because DI constructs QueryCachingBehavior for every query (it only caches
        // [Cacheable] ones). Without an ICachingStrategy, GetPaymentQuery would fail to dispatch.
        services.AddRelayInMemoryCache();

        services.AddRelayExceptionHandling(options =>
        {
            // Dev: expose exception messages. In production you would leave this false so 5xx details
            // and non-Relay 4xx messages stay generic (the Relay exception hierarchy is always safe).
            options.IncludeExceptionDetails = true;

            // ProblemDetails "type" URIs are built as {TypeUrlBase}/{problem-type}.
            options.TypeUrlBase = "https://api.payments.example/problems";

            // Give our own exception a status without subclassing a framework type. The most-derived
            // matching mapping wins, and an exact-type match always wins.
            options.CustomExceptionMappings = new Dictionary<Type, (int StatusCode, string Type, string Title)>
            {
                [typeof(InsufficientFundsException)] =
                    (402, "https://api.payments.example/problems/insufficient-funds", "Insufficient Funds"),
            };
        });

        return services;
    }
}
