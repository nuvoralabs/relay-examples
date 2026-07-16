using Nuvora.Nexus.Relay.Core.Application.Commands;
using Nuvora.Nexus.Relay.Core.Application.Queries;
using Nuvora.Nexus.Relay.Tenancy.Attributes;

namespace Tenancy.Sample;

/// <summary>A trivial result type so the commands/queries have something to return.</summary>
public sealed class Unit;

// [TenantScoped]: must run under a tenant. The enforcement behavior fails closed without one.
[TenantScoped]
public sealed class CreateInvoiceCommand : ICommand<Unit>;

// Undecorated → tenant-scoped BY DEFAULT. Fail-closed is the safe default: forgetting the attribute
// can't accidentally make an operation cross-tenant.
public sealed class ArchiveInvoiceCommand : ICommand<Unit>;

// [GlobalOperation]: an explicit, cross-tenant/system command that runs without a tenant.
[GlobalOperation]
public sealed class RebuildSearchIndexCommand : ICommand;

[TenantScoped]
public sealed class GetInvoiceQuery : IQuery<Unit>;

[GlobalOperation]
public sealed class GetSystemStatusQuery : IQuery<Unit>;
