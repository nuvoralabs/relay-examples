using FluentAssertions;
using Nuvora.Nexus.Relay.EventStore;
using Xunit;

namespace SchemaEvolution.Sample;

/// <summary>
/// Upcasting in isolation — no database. The <see cref="DefaultEventSerializer"/> runs the ordered
/// upcaster chain over the stored JSON before deserializing, so a row written under an old type name and
/// field shape comes back as the current event. (The framework's integration test proves the same path
/// end-to-end through Postgres; here we exercise just the serializer, which is where the work happens.)
/// </summary>
public sealed class UpcastingTests
{
    [Fact]
    public void A_stored_v1_payload_is_upcast_to_the_current_event_on_read()
    {
        var registry = new EventTypeRegistry();
        registry.Register(typeof(InvoiceRaised)); // current type ⇒ resolvable by its name
        var serializer = new DefaultEventSerializer(registry, new IEventUpcaster[] { new InvoiceRaisedV1Upcaster() });

        var id = Guid.NewGuid();
        var storedV1 = new EventData(
            eventId: Guid.NewGuid(),
            streamName: $"invoice-{id}",
            eventType: InvoiceRaisedV1Upcaster.V1TypeName,           // the OLD persisted name
            aggregateId: id,
            aggregateType: "invoice",
            version: 0,
            position: 0,
            timestamp: DateTimeOffset.UtcNow,
            data: $"{{\"InvoiceId\":\"{id}\",\"LegacyAmount\":100}}"); // the OLD field shape

        var restored = serializer.DeserializeEvent(storedV1);

        restored.Should().BeOfType<InvoiceRaised>();
        var invoice = (InvoiceRaised)restored;
        invoice.InvoiceId.Should().Be(id);
        invoice.Amount.Should().Be(100m, "the upcaster renamed LegacyAmount → Amount");
    }

    [Fact]
    public void A_current_event_round_trips_without_any_upcaster_touching_it()
    {
        var registry = new EventTypeRegistry();
        var serializer = new DefaultEventSerializer(registry, new IEventUpcaster[] { new InvoiceRaisedV1Upcaster() });

        var original = new InvoiceRaised(Guid.NewGuid(), 250m);
        var data = serializer.SerializeDomainEvent(original, original.InvoiceId, "invoice", version: 0, position: 0);

        var restored = (InvoiceRaised)serializer.DeserializeEvent(data);

        restored.InvoiceId.Should().Be(original.InvoiceId);
        restored.Amount.Should().Be(250m);
    }
}
