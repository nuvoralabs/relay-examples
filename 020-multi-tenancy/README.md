# Sample 020 — Multi-Tenancy

Companion to **[Article 020 — Multi-Tenancy](../../docs/articles/020-multi-tenancy.md)**.

Two halves of tenant isolation, both exercised without a database:

- **Resolution** ([`ResolutionTests`](./Tenancy.Sample.Tests/ResolutionTests.cs)) — the
  `TenantResolutionMiddleware` reads the tenant from the request (header / claim / subdomain / route),
  maps it through an `InMemoryTenantRegistry`, and publishes it into the ambient
  `ITenantContextAccessor` for the rest of the pipeline.
- **Enforcement** ([`EnforcementTests`](./Tenancy.Sample.Tests/EnforcementTests.cs)) — the tenancy
  pipeline behavior is **fail-closed**: a `[TenantScoped]` message (and, by default, an *undecorated*
  one) with no ambient tenant throws `TenantRequiredException` before its handler runs. Only
  `[GlobalOperation]` opts out.

Production adds the row-level-security storage mode (PostgreSQL `SET LOCAL app.current_tenant`) on top of
this; the article covers it, and the resolution + enforcement shown here are the parts every tenant-aware
service needs first.

```csharp
// production wiring (one call) — resolution middleware + enforcement behaviors + cache-key isolation
services.AddRelayTenancy(o => o.KnownTenants.Add(new TenantInfo(id, "acme")));
app.UseRelayTenantContext();
```

## Test it

```bash
dotnet test samples/020-multi-tenancy/Tenancy.Sample.Tests
```

> Requires the **.NET 10 SDK**. No Docker/database needed.
