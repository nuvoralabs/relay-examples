# Sample 021 — Observability (telemetry)

Companion to **[Article 021 — Observability](../../docs/articles/021-observability.md)**.

Relay is instrumented with **OpenTelemetry-native** primitives out of the box: a single
`ActivitySource` and a single `Meter`, both named `Nuvora.Nexus.Relay` (`RelayTelemetry.SourceName` /
`RelayTelemetry.MeterName`). Executing a command/query records a counter (`relay.commands.executed`,
`relay.queries.executed`), a duration histogram, and a trace span (`command <Name>`).

This sample proves it **without an exporter or a database**: it attaches an in-process `MeterListener`
and `ActivityListener` — exactly the plumbing OpenTelemetry uses under the hood — dispatches a command
through the real `ICommandBus`, and asserts the metric and span were emitted. In production you replace
the listeners with one line:

```csharp
services.AddOpenTelemetry()
    .WithTracing(t => t.AddRelayInstrumentation())   // Nuvora.Nexus.Relay.Diagnostics
    .WithMetrics(m => m.AddRelayInstrumentation());
```

## Test it

```bash
dotnet test samples/021-observability/Observability.Sample.Tests
```

> Requires the **.NET 10 SDK**. No Docker/database needed.
