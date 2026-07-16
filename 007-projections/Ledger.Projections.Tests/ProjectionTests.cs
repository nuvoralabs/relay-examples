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

    [Fact]
    public async Task Accounts_are_flagged_VIP_while_their_balance_is_over_one_million()
    {
        var bus = _fixture.Services.GetRequiredService<ICommandBus>();
        var whale = Guid.NewGuid();
        var minnow = Guid.NewGuid();

        await bus.Execute<OpenAccountCommand, Account>(new OpenAccountCommand(whale, "Grace"), CancellationToken.None);
        await bus.Execute<DepositCommand, Account>(new DepositCommand(whale, 1_500_000m), CancellationToken.None);
        await bus.Execute<OpenAccountCommand, Account>(new OpenAccountCommand(minnow, "Ada"), CancellationToken.None);
        await bus.Execute<DepositCommand, Account>(new DepositCommand(minnow, 500m), CancellationToken.None);

        var host = CreateHost();
        await host.StartAsync(CancellationToken.None);
        try
        {
            var promoted = await LedgerFixture.WaitUntilAsync(async () =>
            {
                using var scope = _fixture.Services.CreateScope();
                var ctx = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();
                var row = await ctx.VipAccounts.AsNoTracking().FirstOrDefaultAsync(r => r.Id == whale);
                return row is { IsVip: true, Balance: 1_500_000m };
            }, TimeSpan.FromSeconds(60));

            promoted.Should().BeTrue("a deposit taking the balance over 1M must flag the account VIP");

            using (var scope = _fixture.Services.CreateScope())
            {
                var ctx = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();

                var vip = await ctx.VipAccounts.AsNoTracking().FirstAsync(r => r.Id == whale);
                vip.PromotedAtUtc.Should().NotBeNull("promotion must record when the threshold was crossed");

                // The query the read model exists for: WHERE is_vip. The minnow's row is working
                // state only — it tracks the balance but never shows up in the VIP list.
                var vipIds = await ctx.VipAccounts.AsNoTracking()
                    .Where(r => r.IsVip).Select(r => r.Id).ToListAsync();
                vipIds.Should().Contain(whale).And.NotContain(minnow);
            }

            // Dropping back under the threshold demotes the account (the flag follows the balance).
            await bus.Execute<WithdrawCommand, Account>(new WithdrawCommand(whale, 600_000m), CancellationToken.None);

            var demoted = await LedgerFixture.WaitUntilAsync(async () =>
            {
                using var scope = _fixture.Services.CreateScope();
                var ctx = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();
                var row = await ctx.VipAccounts.AsNoTracking().FirstOrDefaultAsync(r => r.Id == whale);
                return row is { IsVip: false, Balance: 900_000m, PromotedAtUtc: null };
            }, TimeSpan.FromSeconds(60));

            demoted.Should().BeTrue("a withdrawal taking the balance back under 1M must clear the VIP flag");
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Accounts_with_more_than_25_transactions_in_a_month_are_flagged_highly_active()
    {
        var bus = _fixture.Services.GetRequiredService<ICommandBus>();
        var busy = Guid.NewGuid();
        var quiet = Guid.NewGuid();

        await bus.Execute<OpenAccountCommand, Account>(new OpenAccountCommand(busy, "Linus"), CancellationToken.None);
        await bus.Execute<OpenAccountCommand, Account>(new OpenAccountCommand(quiet, "Barbara"), CancellationToken.None);

        // 26 movements this month — one over the highly-active threshold of 25.
        for (var i = 0; i < 13; i++)
        {
            await bus.Execute<DepositCommand, Account>(new DepositCommand(busy, 100m), CancellationToken.None);
            await bus.Execute<WithdrawCommand, Account>(new WithdrawCommand(busy, 40m), CancellationToken.None);
        }

        await bus.Execute<DepositCommand, Account>(new DepositCommand(quiet, 100m), CancellationToken.None);

        var host = CreateHost();
        await host.StartAsync(CancellationToken.None);
        try
        {
            var counted = await LedgerFixture.WaitUntilAsync(async () =>
            {
                using var scope = _fixture.Services.CreateScope();
                var ctx = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();
                var total = await ctx.AccountActivity.AsNoTracking()
                    .Where(r => r.AccountId == busy).SumAsync(r => r.TransactionCount);
                return total == 26;
            }, TimeSpan.FromSeconds(60));

            counted.Should().BeTrue("the activity projection must count every deposit and withdrawal");

            using var assertScope = _fixture.Services.CreateScope();
            var ctx2 = assertScope.ServiceProvider.GetRequiredService<LedgerDbContext>();

            // The month bucket the events actually landed in (read back rather than computed from
            // "now", so the assertion holds even if the test straddles a month boundary).
            var busyRow = await ctx2.AccountActivity.AsNoTracking()
                .Where(r => r.AccountId == busy).OrderByDescending(r => r.TransactionCount).FirstAsync();

            // The dashboard query: this month's highly active accounts — a single indexed read, no counting.
            var active = await ctx2.AccountActivity.AsNoTracking()
                .Where(r => r.Year == busyRow.Year && r.Month == busyRow.Month && r.IsHighlyActive)
                .Select(r => r.AccountId).ToListAsync();
            active.Should().Contain(busy).And.NotContain(quiet);

            var quietRow = await ctx2.AccountActivity.AsNoTracking().FirstAsync(r => r.AccountId == quiet);
            quietRow.TransactionCount.Should().Be(1);
            quietRow.IsHighlyActive.Should().BeFalse();
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }
}
