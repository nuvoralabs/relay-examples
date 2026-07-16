using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nuvora.Nexus.Relay.Bus;
using Nuvora.Nexus.Relay.Messaging.Core;
using Nuvora.Nexus.Relay.Messaging.InMemory;
using Nuvora.Nexus.Relay.Outbox.Domain;
using Nuvora.Nexus.Relay.Outbox.Processors;
using Xunit;

namespace Reliable.Sample;

[Collection("reliable-sample")]
public sealed class EndToEndTests
{
    private readonly ReliableFixture _fixture;

    public EndToEndTests(ReliableFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task A_committed_outbox_row_is_relayed_to_the_broker_and_delivered_to_a_consumer()
    {
        var orderId = Guid.NewGuid();
        var services = _fixture.Services;

        // 1. A consumer subscribes first, so the broker has a bound queue to route to.
        var consumer = services.GetRequiredService<IMessageConsumer>();
        var received = new ConcurrentBag<MessageBrokerMessage>();
        await consumer.SubscribeAsync("orders", Array.Empty<string>(), (message, _) =>
        {
            received.Add(message);
            return Task.CompletedTask;
        });

        // 2. The command publishes an integration event — committed as an outbox row in its transaction.
        var result = await services.GetRequiredService<ICommandBus>()
            .Execute<PlaceOrderCommand, string>(new PlaceOrderCommand(orderId), CancellationToken.None);
        result.Should().Be("ok");

        // 3. The outbox processor relays committed rows to the broker (the at-least-once delivery step).
        var broker = services.GetRequiredService<InMemoryMessageBroker>();
        var outboxProcessor = new OutboxProcessor(
            services.GetRequiredService<IServiceScopeFactory>(),
            broker,
            Options.Create(new OutboxProcessorOptions()),
            NullLogger<OutboxProcessor>.Instance);
        await outboxProcessor.ProcessPendingMessagesAsync(CancellationToken.None);

        // 4. Deterministic delivery → the subscribed consumer receives the message.
        await broker.DrainAsync();

        received.Should().ContainSingle(m =>
            m.EventType == typeof(OrderPlacedIntegrationEvent).FullName && m.Payload.Contains(orderId.ToString()));

        // 5. The outbox row is now marked Processed (published once, durably).
        using var scope = services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
        var rows = (await ctx.Set<OutboxMessage>().AsNoTracking()
                .Where(m => m.EventType == typeof(OrderPlacedIntegrationEvent).FullName).ToListAsync())
            .Where(m => m.Payload.Contains(orderId.ToString())).ToList();
        rows.Should().ContainSingle();
        rows[0].Status.Should().Be(OutboxMessageStatus.Processed);
    }
}
