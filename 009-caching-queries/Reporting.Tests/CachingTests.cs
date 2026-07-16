using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Nuvora.Nexus.Relay.Bus;
using Reporting;
using Reporting.Catalog;
using Xunit;

namespace Reporting.Tests;

public sealed class CachingTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddReporting();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Identical_cacheable_queries_run_the_handler_only_once()
    {
        using var provider = BuildProvider();
        var queries = provider.GetRequiredService<IQueryBus>();
        var counter = provider.GetRequiredService<QueryExecutionCounter>();

        await queries.Execute<GetCatalogStatsQuery, CatalogStats>(new GetCatalogStatsQuery(), CancellationToken.None);
        await queries.Execute<GetCatalogStatsQuery, CatalogStats>(new GetCatalogStatsQuery(), CancellationToken.None);

        counter.Count.Should().Be(1, "the second identical query is served from the cache");
    }

    [Fact]
    public async Task Invalidation_forces_the_next_query_to_recompute()
    {
        using var provider = BuildProvider();
        var queries = provider.GetRequiredService<IQueryBus>();
        var commands = provider.GetRequiredService<ICommandBus>();
        var counter = provider.GetRequiredService<QueryExecutionCounter>();

        await queries.Execute<GetCatalogStatsQuery, CatalogStats>(new GetCatalogStatsQuery(), CancellationToken.None); // miss → 1
        await commands.Execute<RecordSaleCommand>(new RecordSaleCommand(10m), CancellationToken.None);                  // evicts the cached result
        var stats = await queries.Execute<GetCatalogStatsQuery, CatalogStats>(new GetCatalogStatsQuery(), CancellationToken.None); // miss → 2

        counter.Count.Should().Be(2, "invalidation evicted the cached result, so the handler ran again");
        stats.SalesCount.Should().Be(1);
        stats.TotalRevenue.Should().Be(10m);
    }
}
