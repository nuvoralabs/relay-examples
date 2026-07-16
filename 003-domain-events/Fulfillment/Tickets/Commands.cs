using Nuvora.Nexus.Relay.Bus;
using Nuvora.Nexus.Relay.Core.Application.Commands;

namespace Fulfillment.Tickets;

// Commands change a ticket. Each handler loads or creates the aggregate, calls a behavior method, and
// then publishes the aggregate's uncommitted domain events through IDomainEventBus. [SkipTransaction]
// is set because this sample has no database/unit-of-work.
//
// IMPORTANT: publishing the events by hand is what we do HERE, without a persistence stack. In a real
// event-sourced service (article 005) you do NOT call the bus yourself — the transactional pipeline
// dispatches an aggregate's domain events automatically, inside the same transaction that saves them,
// so the read-model update and the state change either both commit or both roll back. This sample
// makes that dispatch explicit purely so you can see the mechanism with no infrastructure.

[SkipTransaction]
public sealed record OpenTicketCommand(string Subject, string Priority) : ICommand<Guid>;

[SkipTransaction]
public sealed record AssignTicketCommand(Guid TicketId, string Assignee) : ICommand;

[SkipTransaction]
public sealed record CloseTicketCommand(Guid TicketId, string Resolution) : ICommand;

public sealed class OpenTicketCommandHandler(TicketStore tickets, IDomainEventBus events)
    : ICommandHandler<OpenTicketCommand, Guid>
{
    public async Task<Guid> Handle(OpenTicketCommand command, CancellationToken cancellationToken)
    {
        var ticket = Ticket.Open(Guid.NewGuid(), command.Subject, command.Priority);
        tickets.Add(ticket);
        await TicketDomainEvents.PublishAndCommitAsync(ticket, events, cancellationToken);
        return ticket.Id;
    }
}

public sealed class AssignTicketCommandHandler(TicketStore tickets, IDomainEventBus events)
    : ICommandHandler<AssignTicketCommand>
{
    public async Task Handle(AssignTicketCommand command, CancellationToken cancellationToken)
    {
        var ticket = tickets.Get(command.TicketId)
            ?? throw new InvalidOperationException($"Ticket '{command.TicketId}' was not found.");

        ticket.Assign(command.Assignee);
        await TicketDomainEvents.PublishAndCommitAsync(ticket, events, cancellationToken);
    }
}

public sealed class CloseTicketCommandHandler(TicketStore tickets, IDomainEventBus events)
    : ICommandHandler<CloseTicketCommand>
{
    public async Task Handle(CloseTicketCommand command, CancellationToken cancellationToken)
    {
        var ticket = tickets.Get(command.TicketId)
            ?? throw new InvalidOperationException($"Ticket '{command.TicketId}' was not found.");

        ticket.Close(command.Resolution);
        await TicketDomainEvents.PublishAndCommitAsync(ticket, events, cancellationToken);
    }
}

internal static class TicketDomainEvents
{
    /// <summary>
    /// Publishes the aggregate's uncommitted events to their handlers, then marks them committed
    /// (advancing the version and clearing the list). In the full stack the transaction does this.
    /// </summary>
    public static async Task PublishAndCommitAsync(Ticket ticket, IDomainEventBus events, CancellationToken cancellationToken)
    {
        var pending = ticket.GetUncommittedChanges().ToList();
        await events.PublishAll(pending, cancellationToken);
        ticket.MarkChangesAsCommitted();
    }
}
