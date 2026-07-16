using Microsoft.Extensions.DependencyInjection;
using Nuvora.Nexus.Relay.Bus;
using Reporting;
using Reporting.Catalog;

var services = new ServiceCollection();
services.AddLogging();
services.AddReporting();

await using var provider = services.BuildServiceProvider();

var queries = provider.GetRequiredService<IQueryBus>();
var commands = provider.GetRequiredService<ICommandBus>();
var counter = provider.GetRequiredService<QueryExecutionCounter>();
var ct = CancellationToken.None;

await queries.Execute<GetCatalogStatsQuery, CatalogStats>(new GetCatalogStatsQuery(), ct);
await queries.Execute<GetCatalogStatsQuery, CatalogStats>(new GetCatalogStatsQuery(), ct); // served from cache
Console.WriteLine($"After two identical queries, the handler ran {counter.Count} time(s).");

await commands.Execute<RecordSaleCommand>(new RecordSaleCommand(49.99m), ct); // mutates + invalidates
var stats = await queries.Execute<GetCatalogStatsQuery, CatalogStats>(new GetCatalogStatsQuery(), ct); // recomputed
Console.WriteLine($"After a sale + invalidation, the handler ran {counter.Count} time(s); revenue = {stats.TotalRevenue}.");
