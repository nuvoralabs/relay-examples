using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nuvora.Nexus.Relay;
using Nuvora.Nexus.Relay.EventStore.EfCore.DependencyInjection;
using Nuvora.Nexus.Relay.Inbox.EfCore.DependencyInjection;
using Nuvora.Nexus.Relay.Persistence.EfCore.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace Inbox.Sample;

public sealed class InboxFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16").WithDatabase("inbox_sample").WithUsername("postgres").WithPassword("postgres").Build();

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
        services.AddRelayInboxEfCore(); // the IInboxRepository (dedup)
        services.AddRelay(typeof(InboxFixture).Assembly);

        Services = services.BuildServiceProvider();
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<SampleDbContext>().Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        if (Services is not null) await Services.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    public static async Task<bool> WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            if (await condition()) return true;
            await Task.Delay(50);
        }
        return await condition();
    }
}

[CollectionDefinition("inbox-sample")]
public class InboxCollection : ICollectionFixture<InboxFixture> { }
