using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nuvora.Nexus.Relay;
using Nuvora.Nexus.Relay.EventStore;
using Nuvora.Nexus.Relay.EventStore.EfCore.DependencyInjection;
using Nuvora.Nexus.Relay.Persistence.EfCore.DependencyInjection;
using Ordering.Metadata.Ordering;
using Testcontainers.PostgreSql;
using Xunit;

namespace Ordering.Metadata;

/// <summary>
/// Spins up a real PostgreSQL via Testcontainers and wires the DI container exactly like a consuming
/// service: a scoped DbContext, the event store, the unit of work, the event-sourced repository, and the
/// Relay bus. The ONLY thing this sample adds on top of article 005's wiring is two lines — a scoped
/// <see cref="RequestContext"/> and a singleton <see cref="RequestContextEnricher"/>. Registering the
/// enricher is the entire opt-in: from then on the transactional pipeline stamps its metadata onto every
/// appended event.
/// </summary>
public sealed class OrderingFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("ordering_metadata")
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

        services.AddDbContext<OrderingDbContext>(
            (sp, o) => o.UseNpgsql(connectionString),
            contextLifetime: ServiceLifetime.Scoped,
            optionsLifetime: ServiceLifetime.Singleton);

        services.AddRelayEventStoreEfCore<OrderingDbContext>();   // SnapshotEvery defaults to 0 (off)
        services.AddRelayUnitOfWorkEfCore<OrderingDbContext>();
        services.AddRelayEventSourcedRepositoryEfCore();

        // Rich event metadata: the ambient per-command context plus the enricher that reads it. The
        // context is SCOPED (one per command scope, which the handler fills and the enricher reads);
        // the enricher is registered as IEventMetadataEnricher so the pipeline picks it up via
        // GetServices<IEventMetadataEnricher>() and stamps its entries onto every appended event.
        services.AddScoped<RequestContext>();
        services.AddScoped<IEventMetadataEnricher, RequestContextEnricher>();

        services.AddRelay(typeof(OrderingFixture).Assembly);

        Services = services.BuildServiceProvider();

        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<OrderingDbContext>().Database.EnsureCreatedAsync();
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

[CollectionDefinition("ordering-metadata")]
public class OrderingCollection : ICollectionFixture<OrderingFixture>
{
}
