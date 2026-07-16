using Microsoft.EntityFrameworkCore;
using Nuvora.Nexus.Relay.Core.Application.Events;
using Nuvora.Nexus.Relay.EventStore;

namespace Inbox.Sample;

public sealed record OrderPlacedIntegrationEvent : IntegrationEvent
{
    public Guid OrderId { get; init; }
}

/// <summary>The side effect an inbox handler produces — used to prove "handled exactly once".</summary>
public class HandledOrder
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid EventId { get; set; }
}

/// <summary>
/// Consumes an integration event from another service. It only STAGES its effect on the shared
/// DbContext; the inbox processor's unit of work commits that effect together with the dedup row, so a
/// duplicate delivery (at-least-once) is a no-op and a failed handler rolls back BOTH.
/// </summary>
public sealed class OrderPlacedIntegrationEventHandler : IIntegrationEventHandler<OrderPlacedIntegrationEvent>
{
    private readonly DbContext _context;

    public OrderPlacedIntegrationEventHandler(IRelayDbContextAccessor accessor)
        => _context = (DbContext)accessor.DbContext;

    public Task Handle(OrderPlacedIntegrationEvent message, CancellationToken cancellationToken)
    {
        _context.Set<HandledOrder>().Add(new HandledOrder
        {
            Id = Guid.NewGuid(),
            OrderId = message.OrderId,
            EventId = message.EventId,
        });
        return Task.CompletedTask;
    }
}
