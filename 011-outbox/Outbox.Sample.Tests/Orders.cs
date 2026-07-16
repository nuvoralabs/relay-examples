using Nuvora.Nexus.Relay.Bus;
using Nuvora.Nexus.Relay.Core.Application.Commands;
using Nuvora.Nexus.Relay.Core.Application.Events;

namespace Outbox.Sample;

/// <summary>An integration event — a fact this service announces to OTHER services.</summary>
public sealed record OrderPlacedIntegrationEvent : IntegrationEvent
{
    public Guid OrderId { get; init; }
}

public sealed record PlaceOrderCommand(Guid OrderId) : ICommand<string>;

/// <summary>Publishes an integration event. The publish stages an outbox row in the command's transaction.</summary>
public sealed class PlaceOrderCommandHandler(IIntegrationEventBus integrationEventBus)
    : ICommandHandler<PlaceOrderCommand, string>
{
    public async Task<string> Handle(PlaceOrderCommand command, CancellationToken cancellationToken)
    {
        await integrationEventBus.Publish(new OrderPlacedIntegrationEvent { OrderId = command.OrderId }, cancellationToken);
        return "ok";
    }
}

public sealed record PlaceOrderThenFailCommand(Guid OrderId) : ICommand<string>;

/// <summary>Publishes, then throws — proving the staged outbox row rolls back with the command.</summary>
public sealed class PlaceOrderThenFailCommandHandler(IIntegrationEventBus integrationEventBus)
    : ICommandHandler<PlaceOrderThenFailCommand, string>
{
    public async Task<string> Handle(PlaceOrderThenFailCommand command, CancellationToken cancellationToken)
    {
        await integrationEventBus.Publish(new OrderPlacedIntegrationEvent { OrderId = command.OrderId }, cancellationToken);
        throw new InvalidOperationException("boom after publish");
    }
}
