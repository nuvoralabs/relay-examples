using Nuvora.Nexus.Relay.Core.Application.Commands;
using Nuvora.Nexus.Relay.EventStore;

namespace Ledger.TimeTravel.Accounts;

// Commands change an account, exactly as in article 005 — no [SkipTransaction], the transactional
// pipeline appends the aggregate's new events and commits atomically. The write side here is unremarkable;
// what this sample adds is the READ side: the tests rebuild past and current state with IEventSourcedReader
// instead of (or alongside) the write-side IEventSourcedRepository.

public sealed record OpenAccountCommand(Guid Id, string Owner) : ICommand<BankAccount>;

public sealed record DepositCommand(Guid AccountId, decimal Amount) : ICommand<BankAccount>;

public sealed record WithdrawCommand(Guid AccountId, decimal Amount) : ICommand<BankAccount>;

public sealed class OpenAccountCommandHandler : ICommandHandler<OpenAccountCommand, BankAccount>
{
    // A returned aggregate is auto-tracked by the transaction executor, so AccountOpened is appended on commit.
    public Task<BankAccount> Handle(OpenAccountCommand command, CancellationToken cancellationToken)
        => Task.FromResult(BankAccount.Open(command.Id, command.Owner));
}

public sealed class DepositCommandHandler(IEventSourcedRepository<BankAccount, Guid> repository)
    : ICommandHandler<DepositCommand, BankAccount>
{
    public async Task<BankAccount> Handle(DepositCommand command, CancellationToken cancellationToken)
    {
        var account = await repository.GetByIdAsync(command.AccountId, cancellationToken)
            ?? throw new InvalidOperationException($"Account {command.AccountId} not found.");

        account.Deposit(command.Amount);
        repository.Update(account); // track for persistence; new MoneyDeposited event appended on commit
        return account;
    }
}

public sealed class WithdrawCommandHandler(IEventSourcedRepository<BankAccount, Guid> repository)
    : ICommandHandler<WithdrawCommand, BankAccount>
{
    public async Task<BankAccount> Handle(WithdrawCommand command, CancellationToken cancellationToken)
    {
        var account = await repository.GetByIdAsync(command.AccountId, cancellationToken)
            ?? throw new InvalidOperationException($"Account {command.AccountId} not found.");

        account.Withdraw(command.Amount);
        repository.Update(account);
        return account;
    }
}
