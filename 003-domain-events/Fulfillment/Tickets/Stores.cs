using System.Collections.Concurrent;

namespace Fulfillment.Tickets;

/// <summary>
/// In-memory store for ticket aggregates, registered as a singleton. It stands in for an
/// event-sourced repository (article 005) so a command can load an existing ticket, mutate it, and
/// keep its version contiguous across operations — all without a database.
/// </summary>
public sealed class TicketStore
{
    private readonly ConcurrentDictionary<Guid, Ticket> _tickets = new();

    public void Add(Ticket ticket) => _tickets[ticket.Id] = ticket;

    public Ticket? Get(Guid id) => _tickets.GetValueOrDefault(id);
}

/// <summary>A flat, query-friendly view of a ticket, built and maintained by a domain-event handler.</summary>
public sealed record TicketView(Guid Id, string Subject, string Priority, string Status, string? Assignee);

/// <summary>
/// The read model: a separate store that the projector keeps in sync from domain events. This is the
/// "Q" in CQRS in miniature — reads come from here, writes go through the aggregate, and the two are
/// connected by events rather than by sharing a model.
/// </summary>
public sealed class TicketReadModelStore
{
    private readonly ConcurrentDictionary<Guid, TicketView> _views = new();

    public TicketView? Get(Guid id) => _views.GetValueOrDefault(id);

    public void Upsert(TicketView view) => _views[view.Id] = view;

    public void Update(Guid id, Func<TicketView, TicketView> update)
    {
        if (_views.TryGetValue(id, out var existing))
        {
            _views[id] = update(existing);
        }
    }
}

/// <summary>A trivial sink for alerts raised by a domain-event handler (stands in for email/Slack/etc.).</summary>
public sealed class AlertLog
{
    private readonly ConcurrentQueue<string> _alerts = new();

    public void Add(string message) => _alerts.Enqueue(message);

    public IReadOnlyList<string> All => _alerts.ToArray();
}
