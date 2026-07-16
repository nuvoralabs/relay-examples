# Sample 011 — The Outbox Pattern

Companion to **[Article 011 — The Outbox Pattern](../../docs/articles/011-outbox.md)**.

Proves the outbox's core guarantee: an integration event published in a command is staged in the
**same transaction**, so it commits or rolls back atomically with the state change — no dual-write.

- Publishing in a command stages a `Pending` `OutboxMessage` (mirrors the framework's `OutboxAtomicityTests`).
- A failing command rolls the outbox row back (zero rows).
- Publishing *outside* a command scope throws (fail loud, never silently drop the event).

## Run it

```bash
dotnet test samples/011-outbox/Outbox.Sample.Tests
```

> **Requires the .NET 10 SDK and Docker** (Testcontainers `postgres:16`). No broker needed — these
> tests verify the *atomic staging*; relaying to a broker is article 014.
