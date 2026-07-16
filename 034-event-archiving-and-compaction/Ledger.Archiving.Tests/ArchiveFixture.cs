using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nuvora.Nexus.Relay.EventStore.EfCore.DependencyInjection;
using Nuvora.Nexus.Relay.Persistence.EfCore.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace Ledger.Archiving;

/// <summary>
/// Spins up a real PostgreSQL via Testcontainers and wires the DI container exactly like a consuming
/// service: a scoped DbContext for the command/append path, a DbContext *factory* for the archiver's own
/// connection (the archiver runs its copy-then-delete in its own transaction), the event store, the unit
/// of work, and the PostgreSQL event archiver. This is the production layout — the only thing the test
/// adds is the container.
///
/// The archiver requires the factory: <c>EfCoreEventArchiver&lt;TContext&gt;</c> takes an
/// <see cref="IDbContextFactory{TContext}"/> so it can own its transaction independently of any ambient
/// request scope. Registering only <c>AddDbContext</c> (and not <c>AddDbContextFactory</c>) would leave
/// <c>AddRelayEventArchivePostgres</c> with nothing to resolve.
/// </summary>
public sealed class ArchiveFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("ledger_archive")
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

        // Factory for the archiver's independent connection; scoped context (sharing the singleton
        // options) for the append path — the same layout a consuming service uses.
        services.AddDbContextFactory<ArchiveDbContext>(o => o.UseNpgsql(connectionString));
        services.AddDbContext<ArchiveDbContext>(
            (sp, o) => o.UseNpgsql(connectionString),
            contextLifetime: ServiceLifetime.Scoped,
            optionsLifetime: ServiceLifetime.Singleton);

        services.AddRelayEventStoreEfCore<ArchiveDbContext>();   // IEventStore + serializer
        services.AddRelayUnitOfWorkEfCore<ArchiveDbContext>();   // the transaction the append commits in
        services.AddRelayEventArchivePostgres<ArchiveDbContext>(); // IEventArchiver (stream compaction)

        Services = services.BuildServiceProvider();

        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ArchiveDbContext>().Database.EnsureCreatedAsync();
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

[CollectionDefinition("archive")]
public class ArchiveCollection : ICollectionFixture<ArchiveFixture>
{
}
