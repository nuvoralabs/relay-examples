using Nuvora.Nexus.Relay.Core.Application.Commands;
using Nuvora.Nexus.Relay.EventStore;

namespace Ordering.Metadata.Ordering;

// Commands carry the request context (correlation id, actor, tenant) the same way an inbound HTTP
// request or a message envelope would. The handler copies it into the scoped RequestContext before
// touching the aggregate; from there the registered enricher reads it when the pipeline appends the
// events. There is NO [SkipTransaction]: the transactional pipeline appends the order's new events and
// commits atomically, stamping the enriched metadata onto each one.

public sealed record PlaceOrderCommand(
    Guid OrderId,
    string Sku,
    int Quantity,
    string CorrelationId,
    string ActorId,
    string TenantId) : ICommand<Order>;

public sealed record ChangeQuantityCommand(
    Guid OrderId,
    int NewQuantity,
    string CorrelationId,
    string ActorId,
    string TenantId) : ICommand<Order>;

public sealed class PlaceOrderCommandHandler(RequestContext context)
    : ICommandHandler<PlaceOrderCommand, Order>
{
    public Task<Order> Handle(PlaceOrderCommand command, CancellationToken cancellationToken)
    {
        // Populate the ambient context for this command's scope. The enricher (resolved from the same
        // scope) will stamp these onto every event the pipeline appends. CausationId is the command's
        // own id here — in a chain, the previous message's id flows in as the new causation id.
        context.CorrelationId = command.CorrelationId;
        context.CausationId = command.OrderId.ToString();
        context.ActorId = command.ActorId;
        context.TenantId = command.TenantId;

        // The returned aggregate is auto-tracked, so its OrderPlaced event is appended on commit.
        return Task.FromResult(Order.Place(command.OrderId, command.Sku, command.Quantity));
    }
}

public sealed class ChangeQuantityCommandHandler(
    RequestContext context,
    IEventSourcedRepository<Order, Guid> repository)
    : ICommandHandler<ChangeQuantityCommand, Order>
{
    public async Task<Order> Handle(ChangeQuantityCommand command, CancellationToken cancellationToken)
    {
        context.CorrelationId = command.CorrelationId;
        context.CausationId = command.OrderId.ToString();
        context.ActorId = command.ActorId;
        context.TenantId = command.TenantId;

        var order = await repository.GetByIdAsync(command.OrderId, cancellationToken)
            ?? throw new InvalidOperationException($"Order {command.OrderId} not found.");

        order.ChangeQuantity(command.NewQuantity);
        repository.Update(order); // track; new OrderQuantityChanged event appended on commit
        return order;
    }
}
