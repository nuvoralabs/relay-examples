# Sample 029 — Rich Event Metadata

Companion to **[Article 029 — Rich Event Metadata](../../docs/articles/029-rich-event-metadata.md)**.

Builds on **[005 — Event Sourcing Basics](../../samples/005-event-sourcing-basics)**. An event-sourced
`Order` aggregate is unchanged business code, but a custom **`IEventMetadataEnricher`** stamps
cross-cutting context — **correlation id, causation id, actor, tenant** — onto the metadata stored
*alongside* every appended event. The headers can then be read straight back off the event log. It
demonstrates:

- The default metadata (`CommandName`, `Timestamp`) Relay always writes vs. **custom enricher** keys.
- `IEventMetadataEnricher` — registering one in DI is the *entire* opt-in; the transactional pipeline
  then calls it (via `EventMetadata.Build`) on every command's event append.
- An ambient, per-command **scoped `RequestContext`** the enricher reads — the realistic way correlation
  id / actor / tenant arrive from inbound middleware or a message envelope.
- Reading the metadata back via `IEventStore.GetEventsAsync` and asserting the custom header values.

The aggregate (`Order`) and its events are the *same apply-only shape from articles 002/005*: enriching
metadata is a cross-cutting concern, not a domain change.

## Layout (one self-contained test project)

```
Ordering.Metadata.Tests/
  Ordering/Order.cs                  # the event-sourced aggregate + its events
  Ordering/Commands.cs               # commands + handlers (carry & set the request context)
  Ordering/RequestContext.cs         # scoped, per-command ambient context (correlation/actor/tenant)
  Ordering/RequestContextEnricher.cs # the custom IEventMetadataEnricher
  OrderingDbContext.cs               # ApplyRelayEventStore() + ApplyRelaySnapshots()
  OrderingFixture.cs                 # Testcontainers Postgres + DI (005's wiring + 2 enricher lines)
  EventMetadataTests.cs              # stamps every event; framework keys win; per-scope isolation
```

## Run it

This sample is exercised through its tests, which provision a **real PostgreSQL** with
[Testcontainers](https://dotnet.testcontainers.org/) — the metadata is a column on the stored event, so
only a real store demonstrates it honestly.

```bash
dotnet test samples/029-rich-event-metadata/Ordering.Metadata.Tests
```

> **Requires the .NET 10 SDK *and* a running Docker daemon** (Testcontainers starts `postgres:16`).
> To host this in a real service, copy the `OrderingFixture` DI block into your `Program.cs`, point
> `UseNpgsql` at your database, and apply the schema (EF migrations, or the baseline script in
> `libraries/nuvora-nexus-relay/docs/migrations/`). The only lines beyond article 005's wiring are the
> scoped `RequestContext` and the `IEventMetadataEnricher` registration.
