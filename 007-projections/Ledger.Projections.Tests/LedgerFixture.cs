using Ledger.Projections.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nuvora.Nexus.Relay;
using Nuvora.Nexus.Relay.EventStore.EfCore.DependencyInjection;
using Nuvora.Nexus.Relay.Persistence.EfCore.DependencyInjection;
using Nuvora.Nexus.Relay.Projections;
using Nuvora.Nexus.Relay.Projections.EfCore.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace Ledger.Projections;

public sealed class LedgerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("ledger_projections")
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

        services.AddRelayEventStoreEfCore<LedgerDbContext>();
        services.AddRelayUnitOfWorkEfCore<LedgerDbContext>();
        services.AddRelayEventSourcedRepositoryEfCore();

        // The read side: durable checkpoint/dead-letter/failure stores + the projection itself. The
        // failure cache is registered here because the tests construct the projection host by hand
        // (instead of AddRelayProjections, which would register a started hosted service).
        services.AddRelayProjectionCheckpointsEfCore<LedgerDbContext>();
        services.AddSingleton<ProjectionFailureCache>();
        services.AddScoped<IProjection, AccountBalanceProjection>();
        services.AddScoped<IProjection, VipAccountProjection>();
        services.AddScoped<IProjection, AccountActivityProjection>();

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

    /// <summary>Polls until <paramref name="condition"/> is true or the timeout elapses.</summary>
    public static async Task<bool> WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await condition())
            {
                return true;
            }

            await Task.Delay(100);
        }

        return await condition();
    }
}

[CollectionDefinition("ledger-projections")]
public class LedgerCollection : ICollectionFixture<LedgerFixture>
{
}
