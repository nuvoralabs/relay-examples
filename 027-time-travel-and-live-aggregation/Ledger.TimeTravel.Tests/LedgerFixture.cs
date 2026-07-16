using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nuvora.Nexus.Relay;
using Nuvora.Nexus.Relay.EventStore.EfCore.DependencyInjection;
using Nuvora.Nexus.Relay.Persistence.EfCore.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace Ledger.TimeTravel;

/// <summary>
/// Spins up a real PostgreSQL via Testcontainers and wires the DI container exactly like a consuming
/// service: a scoped DbContext (plus a factory for background connections), the event store, the unit of
/// work, the write-side event-sourced repository, and the Relay bus. The only line that this sample adds
/// over article 005 is <c>AddRelayEventSourcedReaderEfCore()</c>, which registers the read-only
/// <c>IEventSourcedReader&lt;,&gt;</c> used for live aggregation and time-travel reads.
/// </summary>
public sealed class LedgerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("ledger_timetravel")
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

        services.AddRelayEventStoreEfCore<LedgerDbContext>();   // SnapshotEvery defaults to 0 (off)
        services.AddRelayUnitOfWorkEfCore<LedgerDbContext>();
        services.AddRelayEventSourcedRepositoryEfCore();        // write-side IEventSourcedRepository<,>
        services.AddRelayEventSourcedReaderEfCore();            // read-side IEventSourcedReader<,> (the star of this sample)
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

[CollectionDefinition("timetravel")]
public class TimeTravelCollection : ICollectionFixture<LedgerFixture>
{
}
