using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Nuvora.Nexus.Relay.Http.Mapping;
using Nuvora.Nexus.Relay.Web;
using Payments.Api;
using Payments.Api.Payments;

namespace Payments.Api.Tests;

internal static class PaymentsTestHost
{
    public static TestServer Create()
    {
        var builder = new WebHostBuilder()
            .UseSetting(WebHostDefaults.ApplicationKey, typeof(AuthorizePaymentCommand).Assembly.GetName().Name)
            .ConfigureServices(services => services.AddPaymentsApi())
            .Configure(app =>
            {
                app.UseRelayExceptionHandling();
                app.UseRouting();
                app.UseEndpoints(endpoints => endpoints.MapRelayEndpoints());
            });

        return new TestServer(builder);
    }
}
