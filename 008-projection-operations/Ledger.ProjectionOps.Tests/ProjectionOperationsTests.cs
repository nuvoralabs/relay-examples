using FluentAssertions;
using Ledger.ProjectionOps.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nuvora.Nexus.Relay.Bus;
using Nuvora.Nexus.Relay.Projections;
using Xunit;

namespace Ledger.ProjectionOps;

[Collection("ledger-projection-ops")]
public sealed class ProjectionOperationsTests
{
    private readonly LedgerFixture _fixture;

    public ProjectionOperationsTests(LedgerFixture fixture) => _fixture = fixture;

    private ProjectionHostedService CreateHost() => new(
        _fixture.Services.GetRequiredService<IServiceScopeFactory>(),
        Options.Create(new ProjectionOptions
        {
            BatchSize = 100,
            PollingInterval = TimeSpan.FromMilliseconds(100),
            MaxEventRetries = 2, // a poison event is attempted twice, then dead-lettered and skipped
        }),
        NullLogger<ProjectionHostedService>.Instance,
        _fixture.Services.GetRequiredService<ProjectionFailureCache>());

    [Fact]
    public async Task A_poison_event_is_dead_lettered_so_the_stream_is_not_wedged()
    {
        var bus = _fixture.Services.GetRequiredService<ICommandBus>();
        var before = Guid.NewGuid();
        var poison = Guid.NewGuid();
        var after = Guid.NewGuid();

        await bus.Execute<OpenAccountCommand, Account>(new OpenAccountCommand(before, "Ada"), CancellationToken.None);
        await bus.Execute<OpenAccountCommand, Account>(new OpenAccountCommand(poison, "POISON"), CancellationToken.None);
        await bus.Execute<OpenAccountCommand, Account>(new OpenAccountCommand(after, "Grace"), CancellationToken.None);

        var host = CreateHost();
        await host.StartAsync(CancellationToken.None);
        try
        {
            // 'after' only projects once the poison event ahead of it is skipped — proving the poison
            // event does not block the stream (no head-of-line blocking).
            var unwedged = await LedgerFixture.WaitUntilAsync(async () =>
            {
                using var scope = _fixture.Services.CreateScope();
                var ctx = scope.ServiceProvider.GetRequiredService<LedgerDbContext>();
                return await ctx.AccountActivity.AsNoTracking().AnyAsync(r => r.Id == after);
            }, TimeSpan.FromSeconds(60));
            unwedged.Should().BeTrue("the poison event must be skipped so later events still project");

            using var assertScope = _fixture.Services.CreateScope();
            var ctx = assertScope.ServiceProvider.GetRequiredService<LedgerDbContext>();
            (await ctx.AccountActivity.AsNoTracking().AnyAsync(r => r.Id == before)).Should().BeTrue();
            (await ctx.AccountActivity.AsNoTracking().AnyAsync(r => r.Id == poison)).Should().BeFalse();

            // The skipped event is preserved in the dead-letter store for inspection / replay.
            var deadLetters = await assertScope.ServiceProvider.GetRequiredService<IProjectionDeadLetterStore>()
                .GetAsync("account-activity");
            deadLetters.Should().Contain(d => d.Payload.Contains("POISON"));
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task A_reset_rebuilds_the_read_model_from_history()
    {
        var bus = _fixture.Services.GetRequiredService<ICommandBus>();
        var id = Guid.NewGuid();
        await bus.Execute<OpenAccountCommand, Account>(new OpenAccountCommand(id, "Rebuild-Me"), CancellationToken.None);

        var host = CreateHost();
        await host.StartAsync(CancellationToken.None);
        try
        {
            (await LedgerFixture.WaitUntilAsync(async () =>
            {
                using var scope = _fixture.Services.CreateScope();
                return await scope.ServiceProvider.GetRequiredService<LedgerDbContext>()
                    .AccountActivity.AsNoTracking().AnyAsync(r => r.Id == id);
            }, TimeSpan.FromSeconds(60))).Should().BeTrue("the initial catch-up must complete");

            // Simulate read-model corruption: delete the row out from under the projection.
            using (var scope = _fixture.Services.CreateScope())
            {
                await scope.ServiceProvider.GetRequiredService<LedgerDbContext>()
                    .AccountActivity.Where(r => r.Id == id).ExecuteDeleteAsync();
            }

            // Reset rewinds the projection's checkpoint to 0; the running host replays history and the
            // idempotent projection repopulates the read model.
            var manager = new ProjectionManager(
                _fixture.Services.GetRequiredService<IServiceScopeFactory>(),
                _fixture.Services.GetRequiredService<ProjectionFailureCache>(),
                NullLogger<ProjectionManager>.Instance);
            await manager.ResetAsync("account-activity", fromPosition: 0);

            (await LedgerFixture.WaitUntilAsync(async () =>
            {
                using var scope = _fixture.Services.CreateScope();
                return await scope.ServiceProvider.GetRequiredService<LedgerDbContext>()
                    .AccountActivity.AsNoTracking().AnyAsync(r => r.Id == id);
            }, TimeSpan.FromSeconds(90))).Should().BeTrue("the rebuild must replay history and repopulate the read model");
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }
}
