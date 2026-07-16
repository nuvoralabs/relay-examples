using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nuvora.Nexus.Relay;
using Nuvora.Nexus.Relay.Cache.InMemory.DependencyInjection;
using Nuvora.Nexus.Relay.EventStore.EfCore.DependencyInjection;
using Nuvora.Nexus.Relay.Outbox.EfCore.DependencyInjection;
using Nuvora.Nexus.Relay.Persistence.EfCore.DependencyInjection;
using Nuvora.Nexus.Relay.Projections;
using Nuvora.Nexus.Relay.Projections.EfCore.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace Storefront.Sample;

/// <summary>
/// Wires the full service the way a production host would: a shared DbContext, the event store + unit of
/// work + event-sourced repository (write side), the outbox (integration events), the projection
/// checkpoint stores (read side), the in-memory query cache, and the Relay bus. The only thing the test
/// adds is the PostgreSQL container and driving the projection host by hand.
/// </summary>
public sealed class StorefrontFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("storefront")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    public ServiceProvider Services { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        var connectionString = _postgres.GetConnectionString();

        var services = new ServiceCollection();
        services.AddLogging();

        services.AddDbContextFactory<StorefrontDbContext>(o => o.UseNpgsql(connectionString));
        services.AddDbContext<StorefrontDbContext>(
            (sp, o) => o.UseNpgsql(connectionString),
            contextLifetime: ServiceLifetime.Scoped,
            optionsLifetime: ServiceLifetime.Singleton);

        // Write side.
        services.AddRelayEventStoreEfCore<StorefrontDbContext>();
        services.AddRelayUnitOfWorkEfCore<StorefrontDbContext>();
        services.AddRelayEventSourcedRepositoryEfCore();
        services.AddRelayOutboxEfCore<StorefrontDbContext>();

        // Read side.
        services.AddRelayProjectionCheckpointsEfCore<StorefrontDbContext>();
        services.AddSingleton<ProjectionFailureCache>();
        services.AddScoped<IProjection, OrderSummaryProjection>();

        // Mediator + query cache.
        services.AddRelay(typeof(StorefrontFixture).Assembly);
        services.AddRelayInMemoryCache();

        Services = services.BuildServiceProvider();

        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<StorefrontDbContext>().Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        if (Services is not null)
        {
            await Services.DisposeAsync();
        }

        await _postgres.DisposeAsync();
    }
}

[CollectionDefinition("storefront")]
public class StorefrontCollection : ICollectionFixture<StorefrontFixture>
{
}
