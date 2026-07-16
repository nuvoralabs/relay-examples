using Catalog.Api;
using Catalog.Api.Catalog;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Nuvora.Nexus.Relay.Http.Mapping;
using Nuvora.Nexus.Relay.Web;

namespace Catalog.Api.Tests;

/// <summary>
/// Builds an in-process <see cref="TestServer"/> around the same service registration the real host
/// uses (<see cref="CatalogServiceCollectionExtensions.AddCatalogServices"/>), so the tests exercise
/// the production wiring without a socket. No database is involved — the catalog is in memory.
/// </summary>
internal static class CatalogTestHost
{
    public static TestServer Create()
    {
        var builder = new WebHostBuilder()
            .UseSetting(WebHostDefaults.ApplicationKey, typeof(CreateProductCommand).Assembly.GetName().Name)
            .ConfigureServices(services => services.AddCatalogServices())
            .Configure(app =>
            {
                app.UseRelayExceptionHandling();
                app.UseRouting();
                app.UseEndpoints(endpoints => endpoints.MapRelayEndpoints());
            });

        return new TestServer(builder);
    }
}
