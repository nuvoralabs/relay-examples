using Nuvora.Nexus.Relay.Core.Application.Commands;
using Nuvora.Nexus.Relay.EventStore;

namespace Ledger.Snapshots.Accounts;

public sealed record OpenAccountCommand(Guid Id, string Owner) : ICommand<Account>;

public sealed record DepositCommand(Guid AccountId, decimal Amount) : ICommand<Account>;

public sealed class OpenAccountCommandHandler : ICommandHandler<OpenAccountCommand, Account>
{
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
        repository.Update(account);
        return account;
    }
}
