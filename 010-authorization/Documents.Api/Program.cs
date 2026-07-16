using Documents.Api;
using Nuvora.Nexus.Relay.Auth.Middleware;
using Nuvora.Nexus.Relay.Http.Mapping;
using Nuvora.Nexus.Relay.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDocumentsApi();

var app = builder.Build();

app.UseRelayExceptionHandling();
app.UseRouting();

// Stand-in for ASP.NET Core authentication: project request headers into HttpContext.User. In
// production this is UseAuthentication() instead, and the rest is identical.
app.Use(async (context, next) =>
{
    DevAuthentication.ApplyFromHeaders(context);
    await next(context);
});

// Bridges HttpContext.User → AuthContext for the authorization pipeline behaviors.
app.UseMiddleware<RelayAuthorizationMiddleware>();

app.UseEndpoints(endpoints => endpoints.MapRelayEndpoints());

app.Run();

public partial class Program;
