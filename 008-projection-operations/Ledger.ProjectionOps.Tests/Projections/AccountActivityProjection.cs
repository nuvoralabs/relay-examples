using Ledger.ProjectionOps.Accounts;
using Microsoft.EntityFrameworkCore;
using Nuvora.Nexus.Relay.EventStore;
using Nuvora.Nexus.Relay.Projections;

namespace Ledger.ProjectionOps.ReadModels;

public class AccountActivityReadModel
{
    public Guid Id { get; set; }
    public string Owner { get; set; } = string.Empty;
}

/// <summary>
/// A projection that deliberately fails for accounts named "POISON" — standing in for a real-world
/// poison event (a bug, a payload the projection can't handle). After the configured number of retries
/// the host dead-letters the event and advances past it, so one bad event cannot wedge the stream. The
/// idempotent insert lets a rebuild (which replays everything) re-apply safely.
/// </summary>
public sealed class AccountActivityProjection : IProjection
{
    private readonly DbContext _context;
    private readonly IEventSerializer _serializer;

    public AccountActivityProjection(IRelayDbContextAccessor accessor, IEventSerializer serializer)
    {
        _context = (DbContext)accessor.DbContext;
        _serializer = serializer;
    }

    public string Name => "account-activity";

    public bool Handles(string eventType) => eventType == typeof(AccountOpened).FullName;

    public async Task ProjectAsync(EventData @event, CancellationToken cancellationToken = default)
    {
        var opened = (AccountOpened)_serializer.DeserializeEvent(@event);

        if (opened.Owner == "POISON")
        {
            throw new InvalidOperationException($"poison event: cannot project account {opened.AccountId}");
        }

        if (!await _context.Set<AccountActivityReadModel>().AnyAsync(r => r.Id == opened.AccountId, cancellationToken))
        {
            _context.Set<AccountActivityReadModel>().Add(new AccountActivityReadModel
            {
                Id = opened.AccountId,
                Owner = opened.Owner,
            });
        }
    }
}
