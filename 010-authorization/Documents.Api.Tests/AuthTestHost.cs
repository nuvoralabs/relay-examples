using Documents.Api;
using Documents.Api.Documents;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Nuvora.Nexus.Relay.Auth.Middleware;
using Nuvora.Nexus.Relay.Http.Mapping;
using Nuvora.Nexus.Relay.Web;

namespace Documents.Api.Tests;

internal static class AuthTestHost
{
    public static TestServer Create()
    {
        var builder = new WebHostBuilder()
            .UseSetting(WebHostDefaults.ApplicationKey, typeof(CreateDocumentCommand).Assembly.GetName().Name)
            .ConfigureServices(services => services.AddDocumentsApi())
            .Configure(app =>
            {
                app.UseRelayExceptionHandling();
                app.UseRouting();
                app.Use(async (context, next) =>
                {
                    DevAuthentication.ApplyFromHeaders(context);
                    await next(context);
                });
                app.UseMiddleware<RelayAuthorizationMiddleware>();
                app.UseEndpoints(endpoints => endpoints.MapRelayEndpoints());
            });

        return new TestServer(builder);
    }
}
