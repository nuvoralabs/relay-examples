using Nuvora.Nexus.Relay.Bus;
using Nuvora.Nexus.Relay.Core.Application.Commands;
using Nuvora.Nexus.Relay.Core.Application.Events;

namespace Reliable.Sample;

public sealed record OrderPlacedIntegrationEvent : IntegrationEvent
{
    public Guid OrderId { get; init; }
}

public sealed record PlaceOrderCommand(Guid OrderId) : ICommand<string>;

/// <summary>Publishes the integration event — staged in the command's transaction as an outbox row.</summary>
public sealed class PlaceOrderCommandHandler(IIntegrationEventBus integrationEventBus)
    : ICommandHandler<PlaceOrderCommand, string>
{
    public async Task<string> Handle(PlaceOrderCommand command, CancellationToken cancellationToken)
    {
        await integrationEventBus.Publish(new OrderPlacedIntegrationEvent { OrderId = command.OrderId }, cancellationToken);
        return "ok";
    }
}
