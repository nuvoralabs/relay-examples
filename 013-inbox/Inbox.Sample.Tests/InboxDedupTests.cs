using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Nuvora.Nexus.Relay.Bus;
using Nuvora.Nexus.Relay.Inbox;
using Nuvora.Nexus.Relay.Inbox.EfCore;
using Nuvora.Nexus.Relay.Messaging.Core;
using Xunit;

namespace Inbox.Sample;

/// <summary>Captures the inbox handler so tests can deliver messages directly — no broker needed.</summary>
internal sealed class FakeMessageConsumer : IMessageConsumer
{
    public Func<MessageBrokerMessage, CancellationToken, Task>? Handler { get; private set; }

    public Task SubscribeAsync(
        string queueName,
        IReadOnlyCollection<string> routingKeys,
        Func<MessageBrokerMessage, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default)
    {
        Handler = handler;
        return Task.CompletedTask;
    }
}

[Collection("inbox-sample")]
public sealed class InboxDedupTests
{
    private readonly InboxFixture _fixture;

    public InboxDedupTests(InboxFixture fixture) => _fixture = fixture;

    private InboxProcessor CreateProcessor(FakeMessageConsumer consumer, string queue) => new(
        consumer,
        _fixture.Services.GetRequiredService<IServiceScopeFactory>(),
        _fixture.Services.GetRequiredService<IRelayScopeAccessor>(),
        Options.Create(new InboxOptions { QueueName = queue }),
        NullLogger<InboxProcessor>.Instance);

    [Fact]
    public async Task A_duplicate_delivery_is_processed_exactly_once()
    {
        var consumer = new FakeMessageConsumer();
        var processor = CreateProcessor(consumer, "inbox-dup");
        await processor.StartAsync(CancellationToken.None);
        try
        {
            (await InboxFixture.WaitUntilAsync(() => Task.FromResult(consumer.Handler is not null), TimeSpan.FromSeconds(10)))
                .Should().BeTrue("the inbox processor must subscribe on startup");

            var @event = new OrderPlacedIntegrationEvent { OrderId = Guid.NewGuid() };
            var message = new MessageBrokerMessage
            {
                MessageId = Guid.NewGuid(),
                EventId = @event.EventId,
                EventType = typeof(OrderPlacedIntegrationEvent).FullName!,
                Payload = JsonConvert.SerializeObject(@event),
                OccurredAt = @event.OccurredAt,
            };

            // At-least-once: the same message is delivered twice.
            await consumer.Handler!(message, CancellationToken.None);
            await consumer.Handler!(message, CancellationToken.None);

            using var scope = _fixture.Services.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
            (await ctx.HandledOrders.AsNoTracking().CountAsync(h => h.OrderId == @event.OrderId))
                .Should().Be(1, "the inbox dedup row makes the second delivery a no-op");
            (await ctx.Set<InboxRecord>().AsNoTracking().CountAsync(r => r.MessageId == @event.EventId))
                .Should().Be(1);
        }
        finally
        {
            await processor.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task A_failing_handler_rolls_back_so_redelivery_can_retry()
    {
        var consumer = new FakeMessageConsumer();
        var processor = CreateProcessor(consumer, "inbox-fail");
        await processor.StartAsync(CancellationToken.None);
        try
        {
            (await InboxFixture.WaitUntilAsync(() => Task.FromResult(consumer.Handler is not null), TimeSpan.FromSeconds(10)))
                .Should().BeTrue();

            // An unresolvable event type makes deserialization throw inside the transaction.
            var message = new MessageBrokerMessage
            {
                MessageId = Guid.NewGuid(),
                EventId = Guid.NewGuid(),
                EventType = "orders.unknown-event.v1",
                Payload = "{}",
                OccurredAt = DateTimeOffset.UtcNow,
            };

            var act = () => consumer.Handler!(message, CancellationToken.None);
            await act.Should().ThrowAsync<InvalidOperationException>();

            using var scope = _fixture.Services.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
            (await ctx.Set<InboxRecord>().AsNoTracking().CountAsync(r => r.MessageId == message.EventId))
                .Should().Be(0, "a failed message must remain unprocessed so redelivery retries it");
        }
        finally
        {
            await processor.StopAsync(CancellationToken.None);
        }
    }
}
