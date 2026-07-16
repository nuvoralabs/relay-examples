# Sample 038 — Telemetry Segmentation (per-tenant / per-dimension metrics)

Companion to **[Article 038 — Telemetry Segmentation](../../docs/articles/038-telemetry-segmentation.md)**.

Relay's command/query metrics are tagged by message *type* out of the box (`relay.queries.executed`
with `query`/`success`). This sample shows how to **segment** those metrics by a business dimension —
without forking the instrumentation — using two opt-in seams:

- **`[MetricTag("region")]`** on a query property → its value is added as a tag to the counter, the
  duration histogram, and the trace span. Always on for messages that carry it.
- **`RelayTelemetryOptions.TagTenant`** → adds the ambient `tenant` tag (from `IRelayTenantAccessor`).
  **Opt-in (default off)** because tenant cardinality is multiplicative.

```csharp
public sealed record GetOrders(Guid TenantId, [property: MetricTag("region")] string Region)
    : IQuery<IReadOnlyList<string>>;

// turn tenant tagging on for hot paths where the tenant set is small/known
services.Configure<RelayTelemetryOptions>(o => o.TagTenant = true);
```

> ⚠️ **Cardinality:** only tag **bounded categorical** fields (region, plan tier, channel, tenant *when
> small*). Per-entity ids (order id, user id) and large tenant fleets belong on **spans/logs**, not
> metric dimensions — they explode your metrics backend.

This sample proves it **without an exporter or a database**: it attaches in-process `MeterListener` /
`ActivityListener` plumbing, dispatches through the real `IQueryBus`, and asserts the `region` and
`tenant` tags land where expected. In production you forward everything with one line per signal
(`AddRelayInstrumentation()` — see sample 021).

## Test it

```bash
dotnet test samples/038-telemetry-segmentation/TelemetrySegmentation.Sample.Tests
```

> Requires the **.NET 10 SDK**. No Docker/database needed.

## See also

- [020 — Multi-tenancy](../../docs/articles/020-multi-tenancy.md) — where the ambient tenant comes from.
- [021 — Observability](../../docs/articles/021-observability.md) — the instrumentation this builds on.
