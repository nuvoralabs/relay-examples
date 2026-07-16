using Nuvora.Nexus.Relay.Bus;
using Nuvora.Nexus.Relay.Core.Application.Commands;
using Nuvora.Nexus.Relay.EventStore;

namespace Storefront.Sample;

public sealed record PlaceOrderCommand(Guid OrderId, string Customer, decimal Total) : ICommand<Order>;

public sealed record MarkOrderPaidCommand(Guid OrderId) : ICommand<Order>;

/// <summary>
/// Creates the aggregate and announces the order to other services. Returning the new aggregate persists
/// its <c>OrderPlaced</c> event; the <c>Publish</c> stages an <c>OrderPlacedNotification</c> on the
/// outbox — both commit atomically in the command's transaction (no dual write).
/// </summary>
public sealed class PlaceOrderCommandHandler(IIntegrationEventBus events) : ICommandHandler<PlaceOrderCommand, Order>
{
    public async Task<Order> Handle(PlaceOrderCommand command, CancellationToken cancellationToken)
    {
        var order = Order.Place(command.OrderId, command.Customer, command.Total);
        await events.Publish(new OrderPlacedNotification { OrderId = command.OrderId }, cancellationToken);
        return order;
    }
}

/// <summary>Loads the aggregate, applies a state change, and stages its new events for the transaction.</summary>
public sealed class MarkOrderPaidCommandHandler(IEventSourcedRepository<Order, Guid> repository)
    : ICommandHandler<MarkOrderPaidCommand, Order>
{
    public async Task<Order> Handle(MarkOrderPaidCommand command, CancellationToken cancellationToken)
    {
        var order = await repository.GetByIdAsync(command.OrderId, cancellationToken)
            ?? throw new InvalidOperationException($"Order {command.OrderId} not found.");

        order.MarkPaid();
        repository.Update(order);
        return order;
    }
}
