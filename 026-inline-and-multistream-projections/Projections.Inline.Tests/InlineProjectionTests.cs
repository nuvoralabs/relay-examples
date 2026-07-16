using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Projections.Inline.Domain;
using Projections.Inline.ReadModels;
using Nuvora.Nexus.Relay.Bus;
using Xunit;

namespace Projections.Inline;

/// <summary>
/// Proves the read-after-write guarantee of an inline projection: the read-model row is ALREADY present in
/// the committed transaction the moment the command returns — no projection host, no WaitUntil, no polling.
/// Because the projection staged its write on the same DbContext the events were appended on, the row and
/// the events committed together.
/// </summary>
[Collection("shop")]
public sealed class InlineProjectionTests
{
    private readonly ShopFixture _fixture;
    private ICommandBus Bus => _fixture.Services.GetRequiredService<ICommandBus>();

    public InlineProjectionTests(ShopFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task The_read_model_is_already_committed_when_the_command_returns()
    {
        var id = Guid.NewGuid();

        await Bus.Execute<PlaceOrderCommand, Order>(
            new PlaceOrderCommand(id, "Ada", 250m), CancellationToken.None);

        // No host, no wait: a fresh scope reads the row that the inline projection committed inside the
        // command's own transaction.
        using var scope = _fixture.Services.CreateScope();
        var row = await scope.ServiceProvider.GetRequiredService<ShopDbContext>()
            .OrderSummaries.AsNoTracking().SingleOrDefaultAsync(r => r.OrderId == id);

        row.Should().NotBeNull();
        row!.Customer.Should().Be("Ada");
        row.Amount.Should().Be(250m);
        row.Status.Should().Be("Placed");
    }

    [Fact]
    public async Task A_later_command_updates_the_same_inline_read_model_row_in_its_own_commit()
    {
        var id = Guid.NewGuid();

        await Bus.Execute<PlaceOrderCommand, Order>(
            new PlaceOrderCommand(id, "Grace", 99m), CancellationToken.None);

        // A second command appends OrderCancelled; the inline projection flips the existing row's status
        // inside that command's transaction. Again, assert immediately — no host, no wait.
        await Bus.Execute<CancelOrderCommand, Order>(
            new CancelOrderCommand(id), CancellationToken.None);

        using var scope = _fixture.Services.CreateScope();
        var read = await scope.ServiceProvider.GetRequiredService<ShopDbContext>()
            .OrderSummaries.AsNoTracking().SingleAsync(r => r.OrderId == id);
        read.Status.Should().Be("Cancelled");
    }
}
