using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Projections.Inline.ReadModels;
using Nuvora.Nexus.Relay;
using Nuvora.Nexus.Relay.EventStore;
using Nuvora.Nexus.Relay.EventStore.EfCore.DependencyInjection;
using Nuvora.Nexus.Relay.Persistence.EfCore.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace Projections.Inline;

/// <summary>
/// Spins up a real PostgreSQL via Testcontainers and wires the DI container like a consuming service: a
/// scoped DbContext (plus a factory for background connections), the event store, the unit of work, the
/// event-sourced repository, and the Relay bus.
///
/// The one new line for article 026 is registering the inline projection as
/// <see cref="IInlineEventProjection"/>: the transaction executor resolves them with
/// <c>GetServices&lt;IInlineEventProjection&gt;()</c>, so registering one is all it takes to make it run
/// inside every command's commit. When none are registered the command path is unchanged. The multi-stream
/// projection is registered as its concrete type so the test can drive it directly (it implements
/// <c>IProjection</c>; in production you would run it through the async host of article 007).
/// </summary>
public sealed class ShopFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("shop_inline")
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

        services.AddDbContextFactory<ShopDbContext>(o => o.UseNpgsql(connectionString));
        services.AddDbContext<ShopDbContext>(
            (sp, o) => o.UseNpgsql(connectionString),
            contextLifetime: ServiceLifetime.Scoped,
            optionsLifetime: ServiceLifetime.Singleton);

        services.AddRelayEventStoreEfCore<ShopDbContext>();
        services.AddRelayUnitOfWorkEfCore<ShopDbContext>();
        services.AddRelayEventSourcedRepositoryEfCore();

        // The inline projection: registered as IInlineEventProjection so the transaction executor applies
        // it inside the commit (read-your-writes). It is scoped because it stages on the scoped DbContext.
        services.AddScoped<IInlineEventProjection, OrderSummaryInlineProjection>();

        // The multi-stream projection: driven directly by the test, so just register the concrete type.
        services.AddScoped<FulfillmentProjection>();

        services.AddRelay(typeof(ShopFixture).Assembly);

        Services = services.BuildServiceProvider();

        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ShopDbContext>().Database.EnsureCreatedAsync();
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

[CollectionDefinition("shop")]
public class ShopCollection : ICollectionFixture<ShopFixture>
{
}
