using Microsoft.Extensions.DependencyInjection;
using Nuvora.Nexus.Relay.Messaging.Core;
using Nuvora.Nexus.Relay.Messaging.InMemory;
using Nuvora.Nexus.Relay.Messaging.InMemory.DependencyInjection;

namespace Transport.Sample;

/// <summary>
/// Demonstrates the transport abstraction — <see cref="IMessageBroker"/> (publish) and
/// <see cref="IMessageConsumer"/> (subscribe) — over the in-memory transport. The in-memory transport is
/// interchangeable with RabbitMQ / Azure Service Bus (<c>AddRelayRabbitMq</c> / <c>AddRelayAzureServiceBus</c>):
/// the same publish/subscribe code, and the outbox/inbox processors, work unchanged across all of them.
/// </summary>
public static class TransportScenario
{
    public static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        // Deterministic delivery: messages are delivered on DrainAsync, so the demo/tests are stable.
        services.AddRelayInMemoryTransport(o => o.DeterministicDelivery = true);
        return services.BuildServiceProvider();
    }

    public static async Task<IReadOnlyList<string>> PublishAndConsumeAsync(IServiceProvider provider, params string[] eventTypes)
    {
        var broker = provider.GetRequiredService<InMemoryMessageBroker>();
        var consumer = provider.GetRequiredService<IMessageConsumer>();

        var received = new List<string>();
        var gate = new object();

        // Subscribe BEFORE publishing so the queue is bound (the broker throws if a publish is unroutable,
        // mirroring RabbitMQ's mandatory-publish behaviour the outbox relies on).
        await consumer.SubscribeAsync("orders", Array.Empty<string>(), (message, _) =>
        {
            lock (gate) received.Add(message.EventType);
            return Task.CompletedTask;
        });

        foreach (var eventType in eventTypes)
        {
            await broker.PublishAsync(new MessageBrokerMessage
            {
                MessageId = Guid.NewGuid(),
                EventType = eventType,
                Payload = "{}",
            });
        }

        await broker.DrainAsync(); // deliver everything deterministically
        return received;
    }
}
