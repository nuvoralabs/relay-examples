using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nuvora.Nexus.Relay.Bus;
using Nuvora.Nexus.Relay.Outbox.Domain;
using Xunit;

namespace Outbox.Sample;

[Collection("outbox-sample")]
public sealed class OutboxAtomicityTests
{
    private readonly OutboxFixture _fixture;
    private ICommandBus Bus => _fixture.Services.GetRequiredService<ICommandBus>();

    public OutboxAtomicityTests(OutboxFixture fixture) => _fixture = fixture;

    private async Task<int> OutboxRowCount(Guid orderId)
    {
        using var scope = _fixture.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
        // jsonb payloads don't support translated Contains — filter the event-type rows client-side.
        var candidates = await ctx.Set<OutboxMessage>().AsNoTracking()
            .Where(m => m.EventType == typeof(OrderPlacedIntegrationEvent).FullName)
            .ToListAsync();
        return candidates.Count(m => m.Payload.Contains(orderId.ToString()));
    }

    [Fact]
    public async Task An_event_published_in_a_command_lands_in_the_outbox_as_pending()
    {
        var orderId = Guid.NewGuid();
        var result = await Bus.Execute<PlaceOrderCommand, string>(new PlaceOrderCommand(orderId), CancellationToken.None);
        result.Should().Be("ok");

        using var scope = _fixture.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<SampleDbContext>();
        var rows = (await ctx.Set<OutboxMessage>().AsNoTracking()
                .Where(m => m.EventType == typeof(OrderPlacedIntegrationEvent).FullName).ToListAsync())
            .Where(m => m.Payload.Contains(orderId.ToString())).ToList();

        rows.Should().ContainSingle();
        rows[0].Status.Should().Be(OutboxMessageStatus.Pending);
    }

    [Fact]
    public async Task A_failing_command_rolls_back_its_outbox_row()
    {
        var orderId = Guid.NewGuid();

        var act = () => Bus.Execute<PlaceOrderThenFailCommand, string>(new PlaceOrderThenFailCommand(orderId), CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>();

        (await OutboxRowCount(orderId)).Should().Be(0,
            "the outbox write must commit and roll back with the command's transaction (no dual write)");
    }

    [Fact]
    public async Task Publishing_outside_a_command_scope_fails_loudly_instead_of_dropping_the_event()
    {
        var bus = _fixture.Services.GetRequiredService<IIntegrationEventBus>();

        // No ambient command transaction → nowhere to atomically stage the outbox row.
        var act = () => bus.Publish(new OrderPlacedIntegrationEvent { OrderId = Guid.NewGuid() }, CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
