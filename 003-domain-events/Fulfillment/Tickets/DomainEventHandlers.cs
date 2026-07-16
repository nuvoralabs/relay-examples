using Nuvora.Nexus.Relay.Core.Domain;

namespace Fulfillment.Tickets;

// Domain-event handlers implement IDomainEventHandler<TEvent> and are discovered + registered by
// AddRelay, exactly like command/query handlers. When a domain event is published, the bus finds every
// handler for that event's runtime type and runs them. The aggregate has no idea these exist — that
// decoupling is the whole point of domain events.

/// <summary>
/// Keeps the ticket read model in sync. One class can handle several event types — here it implements
/// three handler interfaces, so it reacts to the ticket's whole lifecycle.
/// </summary>
public sealed class TicketReadModelProjector(TicketReadModelStore store)
    : IDomainEventHandler<TicketOpened>,
      IDomainEventHandler<TicketAssigned>,
      IDomainEventHandler<TicketClosed>
{
    public Task Handle(TicketOpened message, CancellationToken cancellationToken)
    {
        store.Upsert(new TicketView(message.TicketId, message.Subject, message.Priority, "Open", Assignee: null));
        return Task.CompletedTask;
    }

    public Task Handle(TicketAssigned message, CancellationToken cancellationToken)
    {
        store.Update(message.TicketId, view => view with { Status = "Assigned", Assignee = message.Assignee });
        return Task.CompletedTask;
    }

    public Task Handle(TicketClosed message, CancellationToken cancellationToken)
    {
        store.Update(message.TicketId, view => view with { Status = "Closed" });
        return Task.CompletedTask;
    }
}

/// <summary>
/// A SECOND, independent handler for the same <see cref="TicketOpened"/> event — proof that domain
/// events fan out. Raising an alert is a different concern from maintaining the read model, so it lives
/// in a different handler and could be deployed, tested, or removed without touching the other.
/// </summary>
public sealed class HighPriorityTicketAlerter(AlertLog alerts) : IDomainEventHandler<TicketOpened>
{
    public Task Handle(TicketOpened message, CancellationToken cancellationToken)
    {
        if (string.Equals(message.Priority, "high", StringComparison.OrdinalIgnoreCase))
        {
            alerts.Add($"High-priority ticket opened: \"{message.Subject}\" (correlation {message.CorrelationId})");
        }

        return Task.CompletedTask;
    }
}
