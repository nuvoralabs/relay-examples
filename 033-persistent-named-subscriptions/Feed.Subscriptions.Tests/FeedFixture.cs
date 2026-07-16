using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nuvora.Nexus.Relay.EventStore.EfCore.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace Feed.Subscriptions;

/// <summary>
/// Spins up a real PostgreSQL via Testcontainers and wires the DI container exactly like a consuming
/// service: a DbContext factory (the subscription store takes its own connection per call) and the
/// PostgreSQL-backed <c>ISubscriptionStore</c>. The connection string is exposed so a test can build a
/// <em>second, independent</em> ServiceProvider against the same database — simulating a process restart
/// that reconnects and resumes from the stored position.
/// </summary>
public sealed class FeedFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16")
        .WithDatabase("feed_subscriptions")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    /// <summary>Connection string of the running container — shared by every "process" in the tests.</summary>
    public string ConnectionString { get; private set; } = null!;

    /// <summary>The first process's provider (the subscription store + its DbContext factory).</summary>
    public ServiceProvider Services { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        ConnectionString = _postgres.GetConnectionString();

        Services = BuildProvider(ConnectionString);

        // Create the schema once (relay_events + relay_subscriptions) via the same factory the store uses.
        // A consuming service would use EF migrations or the baseline SQL instead.
        var factory = Services.GetRequiredService<IDbContextFactory<FeedDbContext>>();
        await using var context = await factory.CreateDbContextAsync();
        await context.Database.EnsureCreatedAsync();
    }

    /// <summary>
    /// Build a self-contained provider against <paramref name="connectionString"/> — the production DI
    /// layout for the subscription store. Calling it twice against the same connection string yields two
    /// independent "processes" sharing one database, which is exactly how resume-across-restart is proven.
    /// </summary>
    public static ServiceProvider BuildProvider(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        // The store opens its own connection per call via the factory — the same layout a consuming
        // service uses for background readers.
        services.AddDbContextFactory<FeedDbContext>(o => o.UseNpgsql(connectionString));

        services.AddRelaySubscriptionStorePostgres<FeedDbContext>();   // ISubscriptionStore (singleton)

        return services.BuildServiceProvider();
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

[CollectionDefinition("feed")]
public class FeedCollection : ICollectionFixture<FeedFixture>
{
}
