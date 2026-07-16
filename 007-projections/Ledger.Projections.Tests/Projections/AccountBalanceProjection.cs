using Ledger.Projections.Accounts;
using Microsoft.EntityFrameworkCore;
using Nuvora.Nexus.Relay.EventStore;
using Nuvora.Nexus.Relay.Projections;

namespace Ledger.Projections.ReadModels;

/// <summary>The query-optimised read model: a flat row per account with its current balance.</summary>
public class AccountBalanceReadModel
{
    public Guid Id { get; set; }
    public string Owner { get; set; } = string.Empty;
    public decimal Balance { get; set; }
}

/// <summary>
/// A projection: it subscribes to the event stream and keeps <see cref="AccountBalanceReadModel"/> in
/// sync. The projection host calls <see cref="ProjectAsync"/> for each event it handles and commits the
/// read-model write together with the projection's checkpoint advance — so the checkpoint can never get
/// ahead of the applied events. Writes are staged on the shared DbContext; the projection never calls
/// SaveChanges itself.
/// </summary>
public sealed class AccountBalanceProjection : IProjection
{
    private readonly DbContext _context;
    private readonly IEventSerializer _serializer;

    public AccountBalanceProjection(IRelayDbContextAccessor accessor, IEventSerializer serializer)
    {
        _context = (DbContext)accessor.DbContext;
        _serializer = serializer;
    }

    public string Name => "account-balance";

    public bool Handles(string eventType)
        => eventType == typeof(AccountOpened).FullName
        || eventType == typeof(MoneyDeposited).FullName
        || eventType == typeof(MoneyWithdrawn).FullName;

    public async Task ProjectAsync(EventData @event, CancellationToken cancellationToken = default)
    {
        // The host applies a whole BATCH of events and commits once at the end. So the open/deposit/
        // withdraw events for one account are all staged on this DbContext before any SaveChanges.
        // FindAsync resolves against the change tracker FIRST (then the DB), so a row inserted by an
        // earlier event in THIS batch is visible here — a LINQ query (FirstOrDefault) would hit the
        // database and miss the not-yet-committed row, silently dropping the deposit/withdrawal.
        var accounts = _context.Set<AccountBalanceReadModel>();
        switch (_serializer.DeserializeEvent(@event))
        {
            case AccountOpened e:
                // Idempotent insert: at-least-once delivery (and rebuilds) may re-present an event.
                if (await accounts.FindAsync([e.AccountId], cancellationToken) is null)
                {
                    accounts.Add(new AccountBalanceReadModel { Id = e.AccountId, Owner = e.Owner, Balance = 0m });
                }
                break;

            case MoneyDeposited e:
                {
                    var row = await accounts.FindAsync([e.AccountId], cancellationToken);
                    if (row is not null) row.Balance += e.Amount;
                }
                break;

            case MoneyWithdrawn e:
                {
                    var row = await accounts.FindAsync([e.AccountId], cancellationToken);
                    if (row is not null) row.Balance -= e.Amount;
                }
                break;
        }
    }
}
