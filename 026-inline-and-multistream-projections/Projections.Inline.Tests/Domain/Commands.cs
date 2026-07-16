using Nuvora.Nexus.Relay.Core.Application.Commands;
using Nuvora.Nexus.Relay.EventStore;

namespace Projections.Inline.Domain;

// Commands run in the transactional pipeline (no [SkipTransaction]). Each one appends the aggregate's
// new events on commit. When an IInlineEventProjection is registered, the executor also applies those
// just-appended events to it BEFORE the commit — so its read model lands atomically with the events.

public sealed record PlaceOrderCommand(Guid OrderId, string Customer, decimal Amount) : ICommand<Order>;

public sealed record CancelOrderCommand(Guid OrderId) : ICommand<Order>;

public sealed record DispatchShipmentCommand(Guid ShipmentId, Guid OrderId, string Carrier) : ICommand<Shipment>;

public sealed record DeliverShipmentCommand(Guid ShipmentId) : ICommand<Shipment>;

public sealed class PlaceOrderCommandHandler : ICommandHandler<PlaceOrderCommand, Order>
{
    // The returned aggregate is auto-tracked by the transaction executor, so its OrderPlaced event is
    // appended on commit. No repository call is needed to create.
    public Task<Order> Handle(PlaceOrderCommand command, CancellationToken cancellationToken)
        => Task.FromResult(Order.Place(command.OrderId, command.Customer, command.Amount));
}

public sealed class CancelOrderCommandHandler(IEventSourcedRepository<Order, Guid> repository)
    : ICommandHandler<CancelOrderCommand, Order>
{
    public async Task<Order> Handle(CancelOrderCommand command, CancellationToken cancellationToken)
    {
        var order = await repository.GetByIdAsync(command.OrderId, cancellationToken)
            ?? throw new InvalidOperationException($"Order {command.OrderId} not found.");

        order.Cancel();
        repository.Update(order); // track for persistence; new OrderCancelled event appended on commit
        return order;
    }
}

public sealed class DispatchShipmentCommandHandler : ICommandHandler<DispatchShipmentCommand, Shipment>
{
    public Task<Shipment> Handle(DispatchShipmentCommand command, CancellationToken cancellationToken)
        => Task.FromResult(Shipment.Dispatch(command.ShipmentId, command.OrderId, command.Carrier));
}

public sealed class DeliverShipmentCommandHandler(IEventSourcedRepository<Shipment, Guid> repository)
    : ICommandHandler<DeliverShipmentCommand, Shipment>
{
    public async Task<Shipment> Handle(DeliverShipmentCommand command, CancellationToken cancellationToken)
    {
        var shipment = await repository.GetByIdAsync(command.ShipmentId, cancellationToken)
            ?? throw new InvalidOperationException($"Shipment {command.ShipmentId} not found.");

        shipment.MarkDelivered();
        repository.Update(shipment); // track for persistence; new ShipmentDelivered event appended on commit
        return shipment;
    }
}
