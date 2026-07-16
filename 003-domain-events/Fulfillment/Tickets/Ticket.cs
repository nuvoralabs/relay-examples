using Nuvora.Nexus.Relay.Core.Domain;

namespace Fulfillment.Tickets;

public enum TicketStatus
{
    Open = 0,
    Assigned = 1,
    Closed = 2,
}

/// <summary>
/// A support ticket aggregate. As in article 002, every command method checks invariants and then
/// raises a domain event, and state changes only in <see cref="ApplyEvent"/>. What is new here is that
/// those events are <em>dispatched</em> to handlers (see DomainEventHandlers.cs) so the rest of the
/// system can react — without the ticket depending on any of them.
/// </summary>
[AggregateType("fulfillment.ticket")]
public sealed class Ticket : AggregateRoot<Guid>
{
    public string Subject { get; private set; } = string.Empty;
    public string Priority { get; private set; } = string.Empty;
    public TicketStatus Status { get; private set; } = TicketStatus.Open;
    public string? Assignee { get; private set; }

    protected override bool ApplyEventsOnRaise => true;

    private Ticket() { }

    public static Ticket Open(Guid id, string subject, string priority)
    {
        Guard.AgainstEmptyGuid(id, nameof(id));
        Guard.AgainstNullOrWhiteSpace(subject, nameof(subject));
        Guard.AgainstNullOrWhiteSpace(priority, nameof(priority));

        var ticket = new Ticket();
        // Correlate the whole ticket's event chain by its id, so a reader can group everything that
        // happened to this ticket together.
        ticket.RaiseEvent(new TicketOpened(id, subject.Trim(), priority.Trim().ToLowerInvariant()) { CorrelationId = id });
        return ticket;
    }

    public void Assign(string assignee)
    {
        Guard.AgainstNullOrWhiteSpace(assignee, nameof(assignee));
        Guard.Against(Status == TicketStatus.Closed, "A closed ticket cannot be reassigned.");
        RaiseEvent(new TicketAssigned(Id, assignee.Trim()) { CorrelationId = Id });
    }

    public void Close(string resolution)
    {
        Guard.AgainstNullOrWhiteSpace(resolution, nameof(resolution));
        Guard.Against(Status != TicketStatus.Assigned, "Only an assigned ticket can be closed.");
        RaiseEvent(new TicketClosed(Id, resolution.Trim()) { CorrelationId = Id });
    }

    protected override void ApplyEvent(IDomainEvent domainEvent)
    {
        switch (domainEvent)
        {
            case TicketOpened e:
                SetId(e.TicketId);
                Subject = e.Subject;
                Priority = e.Priority;
                Status = TicketStatus.Open;
                break;
            case TicketAssigned e:
                Assignee = e.Assignee;
                Status = TicketStatus.Assigned;
                break;
            case TicketClosed:
                Status = TicketStatus.Closed;
                break;
        }
    }
}
