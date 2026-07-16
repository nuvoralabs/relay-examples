# Sample 013 — The Inbox Pattern

Companion to **[Article 013 — The Inbox Pattern](https://relay.nuvoralabs.com/articles/inbox/)**.

Idempotent consumption. The `InboxProcessor` records a dedup row (`relay_inbox_messages`) in the **same
transaction** as the handler's effect, so at-least-once delivery becomes exactly-once *processing*.
Mirrors the framework's `InboxTests` (a `FakeMessageConsumer` delivers messages directly — no broker).

- A duplicate delivery is processed exactly once (one effect, one dedup row).
- A failing handler rolls back both effect and dedup row, so redelivery can retry.

## Run it

```bash
dotnet test samples/013-inbox/Inbox.Sample.Tests
```

> **Requires the .NET 10 SDK and Docker** (Testcontainers `postgres:16`).
