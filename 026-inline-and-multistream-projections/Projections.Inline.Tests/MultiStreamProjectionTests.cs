using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Projections.Inline.Domain;
using Projections.Inline.ReadModels;
using Nuvora.Nexus.Relay.Bus;
using Nuvora.Nexus.Relay.EventStore;
using Xunit;

namespace Projections.Inline;

/// <summary>
/// Proves a <see cref="Nuvora.Nexus.Relay.Projections.MultiStreamProjection"/> folds TWO streams into one
/// read model via its typed <c>On&lt;TEvent&gt;</c> handlers. Commands append events to two different
/// aggregates (an order and its shipment), then the test reads the global log and drives the projection —
/// standing in for the async projection host (article 007) so this sample needs no checkpoint store. The
/// assertion is on the single folded row.
/// </summary>
[Collection("shop")]
public sealed class MultiStreamProjectionTests
{
    private readonly ShopFixture _fixture;

    public MultiStreamProjectionTests(ShopFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task It_folds_order_and_shipment_streams_into_one_fulfillment_row()
    {
        var bus = _fixture.Services.GetRequiredService<ICommandBus>();
        var orderId = Guid.NewGuid();
        var shipmentId = Guid.NewGuid();

        // Two streams: the order's, then its shipment's (a separate aggregate type).
        await bus.Execute<PlaceOrderCommand, Order>(
            new PlaceOrderCommand(orderId, "Lin", 480m), CancellationToken.None);
        await bus.Execute<DispatchShipmentCommand, Shipment>(
            new DispatchShipmentCommand(shipmentId, orderId, "DHL"), CancellationToken.None);
        await bus.Execute<DeliverShipmentCommand, Shipment>(
            new DeliverShipmentCommand(shipmentId), CancellationToken.None);

        // Drive the multi-stream projection over the global log, exactly as the async host would: read
        // forward, route each event through ProjectAsync (typed dispatch), then commit the staged writes.
        using var scope = _fixture.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var store = sp.GetRequiredService<IEventStore>();
        var projection = sp.GetRequiredService<FulfillmentProjection>();
        var ctx = sp.GetRequiredService<ShopDbContext>();

        var events = await store.GetAllEventsAsync(fromPosition: 0, maxCount: null, CancellationToken.None);
        foreach (var @event in events.Where(e => projection.Handles(e.EventType)))
        {
            await projection.ProjectAsync(@event, CancellationToken.None);
        }
        await ctx.SaveChangesAsync(CancellationToken.None);

        // One row, fed by handlers across BOTH streams: order data from OrderPlaced, carrier + status from
        // the shipment stream.
        var row = await ctx.Fulfillments.AsNoTracking().SingleAsync(r => r.OrderId == orderId);
        row.Customer.Should().Be("Lin");
        row.Amount.Should().Be(480m);
        row.Carrier.Should().Be("DHL");
        row.FulfillmentStatus.Should().Be("Delivered");
    }
}
