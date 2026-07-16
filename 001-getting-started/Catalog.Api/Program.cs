using Catalog.Api;
using Nuvora.Nexus.Relay.Http.Mapping;
using Nuvora.Nexus.Relay.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCatalogServices();

var app = builder.Build();

// ProblemDetails + correlation id first, so it wraps everything downstream.
app.UseRelayExceptionHandling();

app.UseRouting();
app.UseEndpoints(endpoints =>
{
    // Generates one HTTP endpoint per [RelayHttp*]-annotated command/query in the assembly.
    endpoints.MapRelayEndpoints();
});

app.Run();

// Exposed so integration tests can build a TestServer around the same wiring.
public partial class Program;
