using Nuvora.Nexus.Relay.Core.Application.Commands;

namespace Ledger.ProjectionOps.Accounts;

public sealed record OpenAccountCommand(Guid Id, string Owner) : ICommand<Account>;

public sealed class OpenAccountCommandHandler : ICommandHandler<OpenAccountCommand, Account>
{
    public Task<Account> Handle(OpenAccountCommand command, CancellationToken cancellationToken)
        => Task.FromResult(Account.Open(command.Id, command.Owner));
}
