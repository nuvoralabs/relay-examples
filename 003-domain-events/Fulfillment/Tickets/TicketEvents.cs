using Nuvora.Nexus.Relay.Core.Domain;

namespace Fulfillment.Tickets;

// Domain events describe facts that already happened to a ticket. Other parts of the system react to
// them (here: a read-model projector and an alerter) without the ticket knowing those reactors exist.
// Each event derives from DomainEvent, so it carries an EventId, OccurredAt, a per-aggregate Version,
// and optional CausationId/CorrelationId for tracing a chain of cause and effect.

public sealed record TicketOpened(Guid TicketId, string Subject, string Priority) : DomainEvent
{
    public override Guid AggregateId => TicketId;
}

public sealed record TicketAssigned(Guid TicketId, string Assignee) : DomainEvent
{
    public override Guid AggregateId => TicketId;
}

public sealed record TicketClosed(Guid TicketId, string Resolution) : DomainEvent
{
    public override Guid AggregateId => TicketId;
}
