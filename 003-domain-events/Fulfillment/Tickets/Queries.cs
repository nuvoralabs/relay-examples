using Nuvora.Nexus.Relay.Core.Application.Queries;

namespace Fulfillment.Tickets;

/// <summary>Reads the projected ticket view that the domain-event handler maintains.</summary>
public sealed record GetTicketViewQuery(Guid TicketId) : IQuery<TicketView?>;

public sealed class GetTicketViewQueryHandler(TicketReadModelStore readModel)
    : IQueryHandler<GetTicketViewQuery, TicketView?>
{
    public Task<TicketView?> Handle(GetTicketViewQuery query, CancellationToken cancellationToken)
        => Task.FromResult(readModel.Get(query.TicketId));
}
