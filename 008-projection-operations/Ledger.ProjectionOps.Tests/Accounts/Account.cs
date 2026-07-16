using Nuvora.Nexus.Relay.Core.Domain;

namespace Ledger.ProjectionOps.Accounts;

public sealed record AccountOpened(Guid AccountId, string Owner) : DomainEvent
{
    public override Guid AggregateId => AccountId;
}

/// <summary>
/// A minimal event-sourced account — enough to emit <see cref="AccountOpened"/> events for the
/// projection-operations tests. Opening an account named "POISON" is perfectly valid on the write
/// side; it is the read-side projection that chokes on it (see AccountActivityProjection).
/// </summary>
[AggregateType("ledger.account")]
public sealed class Account : AggregateRoot<Guid>
{
    public string Owner { get; private set; } = string.Empty;

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

    protected override void ApplyEvent(IDomainEvent domainEvent)
    {
        if (domainEvent is AccountOpened e)
        {
            SetId(e.AccountId);
            Owner = e.Owner;
        }
    }
}
