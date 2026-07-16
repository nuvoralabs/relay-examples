using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Nuvora.Nexus.Relay.Bus;
using Nuvora.Nexus.Relay.EventStore;
using Ordering.Metadata.Ordering;
using Xunit;

namespace Ordering.Metadata;

/// <summary>
/// Proves the end-to-end story: a registered <see cref="RequestContextEnricher"/> stamps the request
/// context (correlation id, causation id, actor, tenant) onto the metadata of EVERY appended event, and
/// that metadata can be read straight back off the stored event log via
/// <see cref="IEventStore.GetEventsAsync"/>. These are integration tests against real PostgreSQL — the
/// metadata is a column on the stored event, so only a real store demonstrates it honestly.
/// </summary>
[Collection("ordering-metadata")]
public sealed class EventMetadataTests
{
    private readonly OrderingFixture _fixture;
    private ICommandBus Bus => _fixture.Services.GetRequiredService<ICommandBus>();

    public EventMetadataTests(OrderingFixture fixture) => _fixture = fixture;

    private static JsonElement Metadata(EventData @event)
    {
        @event.Metadata.Should().NotBeNull("every appended event carries serialized metadata");
        return JsonDocument.Parse(@event.Metadata!).RootElement;
    }

    private static string? GetString(EventData @event, string key)
        => Metadata(@event).TryGetProperty(key, out var value) ? value.GetString() : null;

    [Fact]
    public async Task Every_appended_event_carries_the_enriched_metadata()
    {
        var orderId = Guid.NewGuid();
        var correlationId = $"corr-{Guid.NewGuid():N}";

        // Two commands → two events on the same stream, sharing the correlation id.
        await Bus.Execute<PlaceOrderCommand, Order>(
            new PlaceOrderCommand(orderId, "WIDGET-1", 3, correlationId, ActorId: "user-42", TenantId: "tenant-acme"),
            CancellationToken.None);
        await Bus.Execute<ChangeQuantityCommand, Order>(
            new ChangeQuantityCommand(orderId, 5, correlationId, ActorId: "user-42", TenantId: "tenant-acme"),
            CancellationToken.None);

        using var scope = _fixture.Services.CreateScope();
        var events = await scope.ServiceProvider.GetRequiredService<IEventStore>()
            .GetEventsAsync(orderId, 0, CancellationToken.None);

        events.Select(e => e.Version).Should().Equal(0, 1);

        // The custom headers are present on BOTH events, read straight back off the event log.
        events.Should().AllSatisfy(e =>
        {
            GetString(e, RequestContextEnricher.CorrelationIdKey).Should().Be(correlationId);
            GetString(e, RequestContextEnricher.ActorIdKey).Should().Be("user-42");
            GetString(e, RequestContextEnricher.TenantIdKey).Should().Be("tenant-acme");
            GetString(e, RequestContextEnricher.CausationIdKey).Should().Be(orderId.ToString());
        });
    }

    [Fact]
    public async Task Framework_keys_remain_authoritative_alongside_the_custom_headers()
    {
        var orderId = Guid.NewGuid();
        await Bus.Execute<PlaceOrderCommand, Order>(
            new PlaceOrderCommand(orderId, "GADGET-2", 1, $"corr-{Guid.NewGuid():N}", ActorId: "svc-import", TenantId: "tenant-globex"),
            CancellationToken.None);

        using var scope = _fixture.Services.CreateScope();
        var events = await scope.ServiceProvider.GetRequiredService<IEventStore>()
            .GetEventsAsync(orderId, 0, CancellationToken.None);

        var metadata = Metadata(events.Single());

        // The framework's authoritative keys are always present (written last by EventMetadata.Build),
        // sitting next to the enricher's custom headers.
        metadata.GetProperty("CommandName").GetString().Should().Be(nameof(PlaceOrderCommand));
        metadata.TryGetProperty("Timestamp", out _).Should().BeTrue();
        metadata.GetProperty(RequestContextEnricher.ActorIdKey).GetString().Should().Be("svc-import");
        metadata.GetProperty(RequestContextEnricher.TenantIdKey).GetString().Should().Be("tenant-globex");
    }

    [Fact]
    public async Task Different_commands_stamp_different_correlation_ids()
    {
        var firstOrder = Guid.NewGuid();
        var secondOrder = Guid.NewGuid();
        var firstCorrelation = $"corr-{Guid.NewGuid():N}";
        var secondCorrelation = $"corr-{Guid.NewGuid():N}";

        await Bus.Execute<PlaceOrderCommand, Order>(
            new PlaceOrderCommand(firstOrder, "A", 1, firstCorrelation, ActorId: "ada", TenantId: "t1"),
            CancellationToken.None);
        await Bus.Execute<PlaceOrderCommand, Order>(
            new PlaceOrderCommand(secondOrder, "B", 1, secondCorrelation, ActorId: "grace", TenantId: "t2"),
            CancellationToken.None);

        using var scope = _fixture.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<IEventStore>();

        var first = await store.GetEventsAsync(firstOrder, 0, CancellationToken.None);
        var second = await store.GetEventsAsync(secondOrder, 0, CancellationToken.None);

        // The scoped RequestContext gives each command its own context — no bleed-through between scopes.
        GetString(first.Single(), RequestContextEnricher.CorrelationIdKey).Should().Be(firstCorrelation);
        GetString(first.Single(), RequestContextEnricher.ActorIdKey).Should().Be("ada");
        GetString(second.Single(), RequestContextEnricher.CorrelationIdKey).Should().Be(secondCorrelation);
        GetString(second.Single(), RequestContextEnricher.ActorIdKey).Should().Be("grace");
    }
}
