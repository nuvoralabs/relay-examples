using FluentAssertions;
using Nuvora.Nexus.Relay.Tenancy;
using Nuvora.Nexus.Relay.Tenancy.Behaviors;
using Xunit;

namespace Tenancy.Sample;

/// <summary>
/// Tenancy is enforced as a pipeline behavior (priority 1, just after authorization). It is
/// <strong>fail-closed</strong>: a tenant-scoped message with no ambient tenant never reaches its
/// handler — it throws <see cref="TenantRequiredException"/>. Only <c>[GlobalOperation]</c> opts out.
/// These tests drive the behavior directly with a hand-set ambient tenant, exactly like the framework's
/// own enforcement tests.
/// </summary>
public sealed class EnforcementTests
{
    private static ITenantContextAccessor Accessor(Guid? tenantId)
    {
        var accessor = new TenantContextAccessor();
        accessor.Set(tenantId is { } id ? TenantContext.For(id) : TenantContext.None);
        return accessor;
    }

    [Fact]
    public async Task A_tenant_scoped_command_without_a_tenant_fails_closed()
    {
        var behavior = new TenancyEnforcementBehavior<CreateInvoiceCommand, Unit>(Accessor(null));

        var act = () => behavior.Handle(new CreateInvoiceCommand(), default, (_, _) => Task.FromResult(new Unit()));

        await act.Should().ThrowAsync<TenantRequiredException>();
    }

    [Fact]
    public async Task An_undecorated_command_is_tenant_scoped_by_default()
    {
        var behavior = new TenancyEnforcementBehavior<ArchiveInvoiceCommand, Unit>(Accessor(null));

        var act = () => behavior.Handle(new ArchiveInvoiceCommand(), default, (_, _) => Task.FromResult(new Unit()));

        await act.Should().ThrowAsync<TenantRequiredException>();
    }

    [Fact]
    public async Task A_tenant_scoped_command_with_a_tenant_proceeds()
    {
        var behavior = new TenancyEnforcementBehavior<CreateInvoiceCommand, Unit>(Accessor(Guid.NewGuid()));
        var ran = false;

        await behavior.Handle(new CreateInvoiceCommand(), default, (_, _) => { ran = true; return Task.FromResult(new Unit()); });

        ran.Should().BeTrue();
    }

    [Fact]
    public async Task A_global_operation_runs_without_a_tenant()
    {
        var behavior = new TenancyEnforcementBehavior<RebuildSearchIndexCommand>(Accessor(null));
        var ran = false;

        await behavior.Handle(new RebuildSearchIndexCommand(), default, (_, _) => { ran = true; return Task.CompletedTask; });

        ran.Should().BeTrue();
    }

    [Fact]
    public async Task A_tenant_scoped_query_without_a_tenant_fails_closed()
    {
        var behavior = new TenancyQueryEnforcementBehavior<GetInvoiceQuery, Unit>(Accessor(null));

        var act = () => behavior.Handle(new GetInvoiceQuery(), default, (_, _) => Task.FromResult(new Unit()));

        await act.Should().ThrowAsync<TenantRequiredException>();
    }

    [Fact]
    public async Task A_global_query_runs_without_a_tenant()
    {
        var behavior = new TenancyQueryEnforcementBehavior<GetSystemStatusQuery, Unit>(Accessor(null));
        var ran = false;

        await behavior.Handle(new GetSystemStatusQuery(), default, (_, _) => { ran = true; return Task.FromResult(new Unit()); });

        ran.Should().BeTrue();
    }
}
