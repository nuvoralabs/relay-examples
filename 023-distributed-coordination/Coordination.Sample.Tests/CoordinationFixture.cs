using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nuvora.Nexus.Relay.EventStore.EfCore.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace Coordination.Sample;

/// <summary>
/// Spins up a real PostgreSQL and registers the event-store accessor plus the Postgres distributed lock
/// — the minimal wiring a service needs to use <c>IDistributedLock</c>. The lock itself uses
/// <c>pg_advisory_lock</c>, which is enforced by the database across every connection/process.
/// </summary>
public sealed class CoordinationFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("coordination")
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

        // A factory for the lock's dedicated session connections, plus a scoped context for the
        // transaction-scoped lock variant — the same layout a consuming service uses.
        services.AddDbContextFactory<CoordinationDbContext>(o => o.UseNpgsql(connectionString));
        services.AddDbContext<CoordinationDbContext>(
            (sp, o) => o.UseNpgsql(connectionString),
            contextLifetime: ServiceLifetime.Scoped,
            optionsLifetime: ServiceLifetime.Singleton);

        services.AddRelayEventStoreEfCore<CoordinationDbContext>();
        services.AddRelayDistributedLockPostgres<CoordinationDbContext>();

        Services = services.BuildServiceProvider();

        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<CoordinationDbContext>().Database.EnsureCreatedAsync();
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

[CollectionDefinition("coordination")]
public class CoordinationCollection : ICollectionFixture<CoordinationFixture>
{
}
