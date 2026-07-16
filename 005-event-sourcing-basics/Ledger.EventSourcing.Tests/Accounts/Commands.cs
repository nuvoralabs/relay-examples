using Nuvora.Nexus.Relay.Core.Application.Commands;
using Nuvora.Nexus.Relay.EventStore;

namespace Ledger.EventSourcing.Accounts;

// Commands change an account. Note what is NOT here: no [SkipTransaction]. The transactional pipeline
// wraps each command in a database transaction, appends the aggregate's new events to the event store,
// and commits atomically. A handler that returns an AggregateRoot has it tracked automatically; a
// handler that mutates a loaded aggregate tracks it via the repository.

public sealed record OpenAccountCommand(Guid Id, string Owner) : ICommand<Account>;

public sealed record DepositCommand(Guid AccountId, decimal Amount) : ICommand<Account>;

public sealed record WithdrawCommand(Guid AccountId, decimal Amount) : ICommand<Account>;

public sealed class OpenAccountCommandHandler : ICommandHandler<OpenAccountCommand, Account>
{
    // The returned aggregate is auto-tracked by the transaction executor, so its AccountOpened event
    // is appended on commit. No repository call is needed to create.
    public Task<Account> Handle(OpenAccountCommand command, CancellationToken cancellationToken)
        => Task.FromResult(Account.Open(command.Id, command.Owner));
}

public sealed class DepositCommandHandler(IEventSourcedRepository<Account, Guid> repository)
    : ICommandHandler<DepositCommand, Account>
{
    public async Task<Account> Handle(DepositCommand command, CancellationToken cancellationToken)
    {
        var account = await repository.GetByIdAsync(command.AccountId, cancellationToken)
            ?? throw new InvalidOperationException($"Account {command.AccountId} not found.");

        account.Deposit(command.Amount);
        repository.Update(account); // track for persistence; new MoneyDeposited event appended on commit
        return account;
    }
}

public sealed class WithdrawCommandHandler(IEventSourcedRepository<Account, Guid> repository)
    : ICommandHandler<WithdrawCommand, Account>
{
    public async Task<Account> Handle(WithdrawCommand command, CancellationToken cancellationToken)
    {
        var account = await repository.GetByIdAsync(command.AccountId, cancellationToken)
            ?? throw new InvalidOperationException($"Account {command.AccountId} not found.");

        account.Withdraw(command.Amount); // throws DomainException if it would overdraw → whole tx rolls back
        repository.Update(account);
        return account;
    }
}
