using Ledger.Projections.Accounts;
using Microsoft.EntityFrameworkCore;
using Nuvora.Nexus.Relay.EventStore;
using Nuvora.Nexus.Relay.Projections;

namespace Ledger.Projections.ReadModels;

/// <summary>
/// A pre-aggregated activity counter: one row per account per calendar month, counting deposits and
/// withdrawals, flagged once the account is "highly active" (more than 25 transactions that month).
/// The dashboard query is a single indexed read — no counting at query time.
/// </summary>
public class AccountActivityReadModel
{
    public Guid AccountId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public int TransactionCount { get; set; }
    public bool IsHighlyActive { get; set; }
}

/// <summary>
/// An aggregation projection over the same event stream: it buckets transactions by the month they
/// <b>occurred</b> in (the event's <c>OccurredAt</c>, never the wall clock at projection time — a
/// rebuild must put every historical event back into its original bucket). Like the balance increments,
/// the counter is not naturally idempotent; it relies on the host committing the batch and the
/// checkpoint atomically, so an event is never double-counted.
/// </summary>
public sealed class AccountActivityProjection : IProjection
{
    /// <summary>An account is highly active in a month once it has strictly more transactions than this.</summary>
    public const int HighActivityThreshold = 25;

    private readonly DbContext _context;
    private readonly IEventSerializer _serializer;

    public AccountActivityProjection(IRelayDbContextAccessor accessor, IEventSerializer serializer)
    {
        _context = (DbContext)accessor.DbContext;
        _serializer = serializer;
    }

    public string Name => "account-activity";

    // Only movements count as transactions — this projection doesn't care about AccountOpened.
    public bool Handles(string eventType)
        => eventType == typeof(MoneyDeposited).FullName
        || eventType == typeof(MoneyWithdrawn).FullName;

    public async Task ProjectAsync(EventData @event, CancellationToken cancellationToken = default)
    {
        (Guid accountId, DateTimeOffset occurredAt) = _serializer.DeserializeEvent(@event) switch
        {
            MoneyDeposited e => (e.AccountId, e.OccurredAt),
            MoneyWithdrawn e => (e.AccountId, e.OccurredAt),
            _ => (Guid.Empty, default(DateTimeOffset)),
        };
        if (accountId == Guid.Empty) return;

        var occurred = occurredAt.UtcDateTime;
        var activity = _context.Set<AccountActivityReadModel>();

        // FindAsync (change tracker first) with the composite key, so the 2nd..Nth transaction of a
        // month staged in the same batch increments the row the 1st one inserted.
        var row = await activity.FindAsync([accountId, occurred.Year, occurred.Month], cancellationToken);
        if (row is null)
        {
            row = new AccountActivityReadModel { AccountId = accountId, Year = occurred.Year, Month = occurred.Month };
            activity.Add(row);
        }

        row.TransactionCount++;
        row.IsHighlyActive = row.TransactionCount > HighActivityThreshold;
    }
}
