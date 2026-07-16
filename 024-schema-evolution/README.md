# Sample 024 — Schema Evolution

Companion to **[Article 024 — Schema Evolution](https://relay.nuvoralabs.com/articles/schema-evolution/)**.

Events are forever — once persisted, a stream's history must keep deserializing even as the code
evolves. Relay gives you two tools, both shown here **without a database** (pure serializer + registry):

- **Stable names** ([`StableNameTests`](./SchemaEvolution.Sample.Tests/StableNameTests.cs)) —
  `[EventType("samples.order-shipped")]` pins the persisted name so you can freely rename/move the CLR
  type. The `EventTypeRegistry` resolves names registry-only (no `Type.GetType`), and rejects two types
  claiming one name (which would corrupt historical reads).
- **Upcasters** ([`UpcastingTests`](./SchemaEvolution.Sample.Tests/UpcastingTests.cs)) — an
  `IEventUpcaster` rewrites an old payload (old type name, `LegacyAmount` field) into the current shape
  on read. `DefaultEventSerializer` runs the chain before deserialization, so the rest of the system
  only ever sees the current event type.

The framework proves the same upcasting end-to-end through Postgres; this sample isolates the serializer,
which is where the transformation actually happens — so it needs no Docker.

## Test it

```bash
dotnet test samples/024-schema-evolution/SchemaEvolution.Sample.Tests
```

> Requires the **.NET 10 SDK**. No Docker/database needed.
