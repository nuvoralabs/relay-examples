using Newtonsoft.Json.Linq;
using Nuvora.Nexus.Relay.Core.Domain;
using Nuvora.Nexus.Relay.EventStore.Snapshots;

namespace Ledger.Snapshots.Accounts;

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

/// <summary>Serializable snapshot of an account's state.</summary>
public sealed record AccountSnapshot(Guid Id, string Owner, decimal Balance);

/// <summary>
/// The same event-sourced account as article 005, now implementing <see cref="ISnapshotable"/> so the
/// repository can store and restore a state snapshot instead of replaying every event. The
/// <see cref="AppliedFromHistory"/> counter lets the tests prove how many events actually replayed on
/// load — it stays 0 when a head snapshot covers the whole stream.
/// </summary>
[AggregateType("ledger.account")]
public sealed class Account : AggregateRoot<Guid>, ISnapshotable
{
    public string Owner { get; private set; } = string.Empty;
    public decimal Balance { get; private set; }
    public int AppliedFromHistory { get; private set; }

    protected override bool ApplyEventsOnRaise => true;

    private Account() { }

    public static Account Open(Guid id, string owner)
    {
        Guard.AgainstEmptyGuid(id, nameof(id));
        Guard.AgainstNullOrWhiteSpace(owner, nameof(owner));
        var account = new Account();
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

        AppliedFromHistory++;
    }

    public object GetSnapshotState() => new AccountSnapshot(Id, Owner, Balance);

    public void RestoreFromSnapshot(object state, long version)
    {
        // The repository hands the persisted snapshot back as a Newtonsoft JObject.
        var restored = ((JObject)state).ToObject<AccountSnapshot>()!;
        SetId(restored.Id);
        Owner = restored.Owner;
        Balance = restored.Balance;
        Version = version;
    }
}
