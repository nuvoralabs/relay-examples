using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nuvora.Nexus.Relay.Bus;
using Nuvora.Nexus.Relay.Outbox.Domain;
using Nuvora.Nexus.Relay.Projections;
using Xunit;

namespace Storefront.Sample;

/// <summary>
/// The whole stack in one flow: a command event-sources an order AND stages an integration event on the
/// outbox (atomically), a projection builds a read model from the event stream, and a query reads it —
/// the canonical CQRS + event-sourcing + outbox + projections shape Relay is built for.
/// </summary>
[Collection("storefront")]
public sealed class EndToEndTests
{
    private readonly StorefrontFixture _fixture;

    public EndToEndTests(StorefrontFixture fixture) => _fixture = fixture;

    // The projection host is a BackgroundService; the test starts/stops it by hand. In a real service
    // AddRelayProjections() runs it for the app's lifetime.
    private ProjectionHostedService CreateProjectionHost() => new(
        _fixture.Services.GetRequiredService<IServiceScopeFactory>(),
        Options.Create(new ProjectionOptions { BatchSize = 100, PollingInterval = TimeSpan.FromMilliseconds(100) }),
        NullLogger<ProjectionHostedService>.Instance,
        _fixture.Services.GetRequiredService<ProjectionFailureCache>());

    private static async Task<T?> WaitForAsync<T>(Func<Task<T?>> read, Func<T?, bool> done, int timeoutSeconds = 60)
        where T : class
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var value = await read();
            if (done(value)) return value;
            await Task.Delay(100);
        }

        return await read();
    }

    [Fact]
    public async Task An_order_flows_command_to_event_store_to_outbox_to_projection_to_query()
    {
        var id = Guid.NewGuid();
        var commands = _fixture.Services.GetRequiredService<ICommandBus>();
        var queries = _fixture.Services.GetRequiredService<IQueryBus>();

        // 1. Command → event-sourced aggregate (OrderPlaced appended) + outbox notification staged,
        //    both in one transaction.
        await commands.Execute<PlaceOrderCommand, Order>(
            new PlaceOrderCommand(id, "Ada Lovelace", 99.50m), CancellationToken.None);

        using (var scope = _fixture.Services.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<StorefrontDbContext>();
            var staged = (await ctx.Set<OutboxMessage>().AsNoTracking()
                    .Where(m => m.EventType == typeof(OrderPlacedNotification).FullName).ToListAsync())
                .Where(m => m.Payload.Contains(id.ToString())).ToList();

            staged.Should().ContainSingle("the integration event is staged atomically with the order event");
            staged[0].Status.Should().Be(OutboxMessageStatus.Pending);
        }

        // 2. The projection host catches up; read the model through the query bus.
        var host = CreateProjectionHost();
        await host.StartAsync(CancellationToken.None);
        try
        {
            var placed = await WaitForAsync(
                () => queries.Execute<GetOrderSummaryQuery, OrderSummary?>(new GetOrderSummaryQuery(id), CancellationToken.None),
                s => s is { Status: "Placed" });

            placed.Should().NotBeNull("the projection must build the read model from the event stream");
            placed!.Customer.Should().Be("Ada Lovelace");
            placed.Total.Should().Be(99.50m);

            // 3. A follow-up command propagates through the same pipeline to the read model.
            await commands.Execute<MarkOrderPaidCommand, Order>(new MarkOrderPaidCommand(id), CancellationToken.None);

            var paid = await WaitForAsync(
                () => queries.Execute<GetOrderSummaryQuery, OrderSummary?>(new GetOrderSummaryQuery(id), CancellationToken.None),
                s => s is { Status: "Paid" });

            paid!.Status.Should().Be("Paid", "the OrderPaid event must project onto the existing read-model row");
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }
}
