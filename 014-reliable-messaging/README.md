# Sample 014 — Reliable Messaging (End to End)

Companion to **[Article 014 — Reliable Messaging](../../docs/articles/014-reliable-messaging.md)**.

Ties together the outbox (011), the transport (012), and the inbox (013) into one flow: a command
publishes an integration event → it is staged as a committed **outbox** row → the **outbox processor**
relays it to the **broker** → a **consumer** receives it. End-to-end over real PostgreSQL + the
in-memory transport (deterministic, so no broker container needed).

The test asserts the message reaches a subscribed consumer with the right payload, and that the outbox
row transitions to `Processed` — the at-least-once delivery guarantee from a committed outbox.

## Run it

```bash
dotnet test samples/014-reliable-messaging/Reliable.Sample.Tests
```

> **Requires the .NET 10 SDK and Docker** (Testcontainers `postgres:16`). The transport is in-memory;
> swapping `AddRelayRabbitMq` (plus a RabbitMQ container) changes none of the command/outbox/inbox code.
