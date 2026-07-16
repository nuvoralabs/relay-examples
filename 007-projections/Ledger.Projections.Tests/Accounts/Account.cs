using Nuvora.Nexus.Relay.Core.Domain;

namespace Ledger.Projections.Accounts;

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

[AggregateType("ledger.account")]
public sealed class Account : AggregateRoot<Guid>
{
    public string Owner { get; private set; } = string.Empty;
    public decimal Balance { get; private set; }

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
    }
}
