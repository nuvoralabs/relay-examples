using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nuvora.Nexus.Relay;
using Nuvora.Nexus.Relay.EventStore.EfCore.DependencyInjection;
using Nuvora.Nexus.Relay.Messaging.InMemory.DependencyInjection;
using Nuvora.Nexus.Relay.Outbox.EfCore.DependencyInjection;
using Nuvora.Nexus.Relay.Persistence.EfCore.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace Reliable.Sample;

public sealed class ReliableFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16").WithDatabase("reliable_sample").WithUsername("postgres").WithPassword("postgres").Build();

    public ServiceProvider Services { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        var cs = _postgres.GetConnectionString();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContextFactory<SampleDbContext>(o => o.UseNpgsql(cs));
        services.AddDbContext<SampleDbContext>(
            (sp, o) => o.UseNpgsql(cs), contextLifetime: ServiceLifetime.Scoped, optionsLifetime: ServiceLifetime.Singleton);

        services.AddRelayEventStoreEfCore<SampleDbContext>();
        services.AddRelayUnitOfWorkEfCore<SampleDbContext>();
        services.AddRelayEventSourcedRepositoryEfCore();
        services.AddRelayOutboxEfCore<SampleDbContext>();

        // The broker the outbox publishes to. In-memory here; swapping AddRelayRabbitMq changes nothing
        // about the outbox/command code. Deterministic delivery makes the end-to-end test stable.
        services.AddRelayInMemoryTransport(o => o.DeterministicDelivery = true);

        services.AddRelay(typeof(ReliableFixture).Assembly);

        Services = services.BuildServiceProvider();
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<SampleDbContext>().Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        if (Services is not null) await Services.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

[CollectionDefinition("reliable-sample")]
public class ReliableCollection : ICollectionFixture<ReliableFixture> { }
