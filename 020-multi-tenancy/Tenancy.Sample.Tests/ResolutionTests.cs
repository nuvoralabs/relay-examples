using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Nuvora.Nexus.Relay.Tenancy;
using Nuvora.Nexus.Relay.Tenancy.Middleware;
using Nuvora.Nexus.Relay.Tenancy.Resolution;
using Xunit;

namespace Tenancy.Sample;

/// <summary>
/// Before enforcement can run, the ambient tenant has to be established from the request. The
/// <see cref="TenantResolutionMiddleware"/> tries each configured source (header / claim / subdomain /
/// route), maps the identifier to a known tenant via the registry, and publishes it into the ambient
/// <see cref="ITenantContextAccessor"/> for the rest of the pipeline. The tenant is observed from inside
/// <c>next</c> — exactly where the behaviors and handlers read it.
/// </summary>
public sealed class ResolutionTests
{
    private static async Task<TenantContext> ResolveAsync(
        HttpContext context, RelayTenancyOptions options, params TenantInfo[] tenants)
    {
        var accessor = new TenantContextAccessor();
        var observed = TenantContext.None;
        var resolvers = new ITenantResolver[]
        {
            new HeaderTenantResolver(options),
            new ClaimTenantResolver(options),
            new SubdomainTenantResolver(),
            new RouteTenantResolver(options),
        };

        var middleware = new TenantResolutionMiddleware(
            _ => { observed = accessor.Current; return Task.CompletedTask; },
            options, resolvers, new InMemoryTenantRegistry(tenants), accessor,
            NullLogger<TenantResolutionMiddleware>.Instance);

        await middleware.InvokeAsync(context);
        return observed;
    }

    [Fact]
    public async Task A_known_tenant_resolves_from_the_header()
    {
        var id = Guid.NewGuid();
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["X-Tenant-Id"] = "acme";

        var observed = await ResolveAsync(ctx, new RelayTenancyOptions(), new TenantInfo(id, "acme"));

        observed.TenantId.Should().Be(id);
    }

    [Fact]
    public async Task An_unknown_guid_identifier_falls_back_to_itself()
    {
        var id = Guid.NewGuid();
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["X-Tenant-Id"] = id.ToString();

        // Empty registry: a raw GUID is still accepted as a tenant id (fallback on by default).
        var observed = await ResolveAsync(ctx, new RelayTenancyOptions());

        observed.TenantId.Should().Be(id);
    }

    [Fact]
    public async Task An_unknown_non_guid_identifier_stays_global()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["X-Tenant-Id"] = "ghost";

        var observed = await ResolveAsync(ctx, new RelayTenancyOptions { AllowGuidIdentifierFallback = false });

        observed.HasTenant.Should().BeFalse();
    }
}
