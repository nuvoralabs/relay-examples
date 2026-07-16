using Ledger.Projections.Accounts;
using Microsoft.EntityFrameworkCore;
using Nuvora.Nexus.Relay.EventStore;
using Nuvora.Nexus.Relay.Projections;

namespace Ledger.Projections.ReadModels;

/// <summary>
/// The VIP read model: one row per account, flagged while its balance is over the VIP threshold.
/// Queries read <c>WHERE is_vip</c> — the sub-threshold rows are the projection's working state, kept
/// because events carry deltas (not totals), so detecting the moment an account crosses the threshold
/// requires knowing every account's running balance.
/// </summary>
public class VipAccountReadModel
{
    public Guid Id { get; set; }
    public string Owner { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public bool IsVip { get; set; }
    public DateTimeOffset? PromotedAtUtc { get; set; }
}

/// <summary>
/// A second projection over the same events as <see cref="AccountBalanceProjection"/>, maintaining a
/// different shape: which accounts are VIP (balance over 1M) right now, and since when. Each projection
/// is fully independent — its own checkpoint, its own table, its own catch-up — which is the point:
/// one read model per query shape.
/// </summary>
public sealed class VipAccountProjection : IProjection
{
    /// <summary>An account is VIP while its balance is strictly over this.</summary>
    public const decimal VipThreshold = 1_000_000m;

    private readonly DbContext _context;
    private readonly IEventSerializer _serializer;

    public VipAccountProjection(IRelayDbContextAccessor accessor, IEventSerializer serializer)
    {
        _context = (DbContext)accessor.DbContext;
        _serializer = serializer;
    }

    public string Name => "vip-accounts";

    public bool Handles(string eventType)
        => eventType == typeof(AccountOpened).FullName
        || eventType == typeof(MoneyDeposited).FullName
        || eventType == typeof(MoneyWithdrawn).FullName;

    public async Task ProjectAsync(EventData @event, CancellationToken cancellationToken = default)
    {
        // Same FindAsync-not-LINQ rule as AccountBalanceProjection: the host stages a whole batch on
        // this DbContext before committing, and only the change tracker sees rows from earlier in it.
        var accounts = _context.Set<VipAccountReadModel>();
        switch (_serializer.DeserializeEvent(@event))
        {
            case AccountOpened e:
                if (await accounts.FindAsync([e.AccountId], cancellationToken) is null)
                {
                    accounts.Add(new VipAccountReadModel { Id = e.AccountId, Owner = e.Owner, Balance = 0m });
                }
                break;

            case MoneyDeposited e:
                {
                    var row = await accounts.FindAsync([e.AccountId], cancellationToken);
                    if (row is not null) Reclassify(row, row.Balance + e.Amount, e.OccurredAt);
                }
                break;

            case MoneyWithdrawn e:
                {
                    var row = await accounts.FindAsync([e.AccountId], cancellationToken);
                    if (row is not null) Reclassify(row, row.Balance - e.Amount, e.OccurredAt);
                }
                break;
        }
    }

    private static void Reclassify(VipAccountReadModel row, decimal newBalance, DateTimeOffset asOf)
    {
        var wasVip = row.IsVip;
        row.Balance = newBalance;
        row.IsVip = newBalance > VipThreshold;
        // PromotedAtUtc comes from the event's OccurredAt, not the wall clock — a rebuild months from
        // now must land on the same answer as the original catch-up.
        if (row.IsVip && !wasVip) row.PromotedAtUtc = asOf;
        if (!row.IsVip && wasVip) row.PromotedAtUtc = null;
    }
}
