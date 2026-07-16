using Nuvora.Nexus.Relay.Core.Domain;

namespace Ledger.TimeTravel.Accounts;

// Domain events — the SOURCE OF TRUTH for an account. Nothing else is stored; state at any point in time
// is derived by folding these up to that point. They carry primitives so they serialise cleanly into the
// event store, and each event's OccurredAt timestamp is what time-travel "as of a timestamp" reads filter
// on (the event store persists it as the row's OccurredAt and the reader compares Timestamp <= asOf).

public sealed record AccountOpened(Guid AccountId, string Owner) : DomainEvent
{
    public override Guid AggregateId => AccountId;
}

public sealed record MoneyDeposited(Guid AccountId, decimal Amount) : DomainEvent
{
    public override Guid AggregateId => AccountId;
}

public sealed record MoneyWithdrawn(Guid AccountId, decimal Amount) : DomainEvent
{
    public override Guid AggregateId => AccountId;
}

/// <summary>
/// An event-sourced bank account, the same apply-only aggregate shape as article 005 — every command
/// checks invariants then raises an event, and state changes only in <see cref="ApplyEvent"/>. This
/// sample never changes the domain code to demonstrate the reader; the point of the
/// <c>IEventSourcedReader</c> is that <em>any</em> historical state is derivable from this stream by
/// folding a prefix of it, with no per-state storage and no read model.
/// </summary>
[AggregateType("ledger.timetravel.account")]
public sealed class BankAccount : AggregateRoot<Guid>
{
    public string Owner { get; private set; } = string.Empty;
    public decimal Balance { get; private set; }

    protected override bool ApplyEventsOnRaise => true;

    // Parameterless ctor used to rehydrate from history (both the repository and the reader use it).
    private BankAccount() { }

    public static BankAccount Open(Guid id, string owner)
    {
        Guard.AgainstEmptyGuid(id, nameof(id));
        Guard.AgainstNullOrWhiteSpace(owner, nameof(owner));

        var account = new BankAccount();
        account.RaiseEvent(new AccountOpened(id, owner.Trim()));
        return account;
    }

    public void Deposit(decimal amount)
    {
        Guard.AgainstNegativeOrZero(amount, nameof(amount));
        RaiseEvent(new MoneyDeposited(Id, amount));
    }

    public void Withdraw(decimal amount)
    {
        Guard.AgainstNegativeOrZero(amount, nameof(amount));
        Guard.Against(amount > Balance, $"Insufficient funds: cannot withdraw {amount} from a balance of {Balance}.");
        RaiseEvent(new MoneyWithdrawn(Id, amount));
    }

    protected override void ApplyEvent(IDomainEvent domainEvent)
    {
        switch (domainEvent)
        {
            case AccountOpened e:
                SetId(e.AccountId);
                Owner = e.Owner;
                Balance = 0m;
                break;
            case MoneyDeposited e:
                Balance += e.Amount;
                break;
            case MoneyWithdrawn e:
                Balance -= e.Amount;
                break;
        }
    }
}
