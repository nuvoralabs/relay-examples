using FluentAssertions;
using Ledger.Projections.Accounts;
using Ledger.Projections.ReadModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nuvora.Nexus.Relay.Bus;
using Nuvora.Nexus.Relay.Projections;
using Xunit;

namespace Ledger.Projections;

[Collection("ledger-projections")]
public sealed class ProjectionTests
{
    private readonly LedgerFixture _fixture;

    public ProjectionTests(LedgerFixture fixture) => _fixture = fixture;

    // The projection host is a BackgroundService; the test constructs it directly so it can start and
    // stop it deterministically. In a real service you call AddRelayProjections() and the host runs for
    // the app's lifetime.
    private ProjectionHostedService CreateHost() => new(
        _fixture.Services.GetRequiredService<IServiceScopeFactory>(),
        Options.Create(new ProjectionOptions
        {
            BatchSize = 100,
            PollingInterval = TimeSpan.FromMilliseconds(100),
        }),
        NullLogger<ProjectionHostedService>.Instance,
        _fixture.Services.GetRequiredService<ProjectionFailureCache>());

    [Fact]
    public async Task The_balance_read_model_catches_up_to_the_event_stream()
    {
        var bus = _fixture.Services.GetRequiredService<ICommandBus>();
        var id = Guid.NewGuid();

        await bus.Execute<OpenAccountCommand, Account>(new OpenAccountCommand(id, "Ada"), CancellationToken.None);
        await bus.Execute<DepositCommand, Account>(new DepositCommand(id, 100m), CancellationToken.None);
        await bus.Execute<WithdrawCommand, Account>(new WithdrawCommand(id, 30m), CancellationToken.None);

        var host = CreateHost();
        await host.StartAsync(CancellationToken.None);
        try
        {
            var projected = await LedgerFixture.WaitUntilAsync(async () =>
            {
                using var scope = _fixture.Services.CreateScope();
                var ctx = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();
                var row = await ctx.AccountBalances.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
                return row is { Balance: 70m }; // 100 deposited − 30 withdrawn, folded by the projection
            }, TimeSpan.FromSeconds(60));

            projected.Should().BeTrue("the projection must catch up and reflect the final balance");

            using var assertScope = _fixture.Services.CreateScope();
            var read = await assertScope.ServiceProvider.GetRequiredService<LedgerDbContext>()
                .AccountBalances.AsNoTracking().FirstAsync(r => r.Id == id);
            read.Owner.Should().Be("Ada");
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }
}
