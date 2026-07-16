using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nuvora.Nexus.Relay;
using Nuvora.Nexus.Relay.EventStore.EfCore.DependencyInjection;
using Nuvora.Nexus.Relay.Persistence.EfCore.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace Ledger.Snapshots;

public sealed class LedgerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("ledger_snapshots")
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

        services.AddDbContextFactory<LedgerDbContext>(o => o.UseNpgsql(connectionString));
        services.AddDbContext<LedgerDbContext>(
            (sp, o) => o.UseNpgsql(connectionString),
            contextLifetime: ServiceLifetime.Scoped,
            optionsLifetime: ServiceLifetime.Singleton);

        // Snapshot every 2 events: a command that advances an aggregate across a multiple of 2 writes a
        // snapshot at that version. The previous snapshot is kept as a fallback (KeepSnapshots defaults to 2).
        services.AddRelayEventStoreEfCore<LedgerDbContext>(s => s.SnapshotEvery = 2);
        services.AddRelayUnitOfWorkEfCore<LedgerDbContext>();
        services.AddRelayEventSourcedRepositoryEfCore();
        services.AddRelay(typeof(LedgerFixture).Assembly);

        Services = services.BuildServiceProvider();

        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<LedgerDbContext>().Database.EnsureCreatedAsync();
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

[CollectionDefinition("ledger-snapshots")]
public class LedgerCollection : ICollectionFixture<LedgerFixture>
{
}
