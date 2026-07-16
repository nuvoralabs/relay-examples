using Nuvora.Nexus.Relay.Http.Mapping;
using Nuvora.Nexus.Relay.Web;
using Payments.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPaymentsApi();

var app = builder.Build();

// UseRelayExceptionHandling adds the correlation-id middleware (so every response carries a traceId)
// and the global exception handler (which maps thrown exceptions to RFC 7807 ProblemDetails). It must
// wrap the endpoints, so it goes first.
app.UseRelayExceptionHandling();

app.UseRouting();
app.UseEndpoints(endpoints => endpoints.MapRelayEndpoints());

app.Run();

public partial class Program;
