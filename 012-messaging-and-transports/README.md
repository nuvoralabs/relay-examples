# Sample 012 — Messaging & Transports

Companion to **[Article 012 — Messaging & Transports](../../docs/articles/012-messaging-and-transports.md)**.

The transport abstraction — `IMessageBroker` (publish) and `IMessageConsumer` (subscribe) — over the
**in-memory transport**, which is interchangeable with RabbitMQ / Azure Service Bus. A subscribed
consumer receives every published message; deterministic delivery (`DrainAsync`) makes it testable
without Docker.

## Run it

```bash
dotnet run  --project samples/012-messaging-and-transports/Transport.Sample
dotnet test samples/012-messaging-and-transports/Transport.Sample.Tests
```

```
Consumer received 2 message(s): order.placed, order.shipped
```

> Requires the **.NET 10 SDK**. No Docker/database needed. Swap `AddRelayInMemoryTransport` for
> `AddRelayRabbitMq(...)` and the publish/subscribe code is unchanged.
