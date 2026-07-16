using FluentAssertions;
using Nuvora.Nexus.Relay.EventStore;
using Xunit;

namespace SchemaEvolution.Sample;

/// <summary>
/// Stable event-type names decouple what's persisted from the CLR type. The <see cref="EventTypeRegistry"/>
/// is a bidirectional map: <c>[EventType("...")]</c> sets the stored name, and resolution is
/// registry-only (no <c>Type.GetType</c> fallback), so database content can never pick an arbitrary type.
/// </summary>
public sealed class StableNameTests
{
    [Fact]
    public void An_event_type_attribute_pins_the_persisted_name_and_round_trips()
    {
        var registry = new EventTypeRegistry();

        var name = registry.GetTypeName(typeof(OrderShipped));
        name.Should().Be("samples.order-shipped");

        registry.TryResolveClrType("samples.order-shipped", out var resolved).Should().BeTrue();
        resolved.Should().Be(typeof(OrderShipped));
    }

    [Fact]
    public void An_unknown_name_does_not_resolve()
        => new EventTypeRegistry().TryResolveClrType("nope.not-registered", out _).Should().BeFalse();

    [Fact]
    public void Two_types_under_one_name_is_rejected_so_history_cannot_deserialize_into_the_wrong_type()
    {
        var registry = new EventTypeRegistry();
        registry.Register(typeof(OrderShipped)); // claims "samples.order-shipped"

        var act = () => registry.Register(typeof(InvoiceRaised), "samples.order-shipped");

        act.Should().Throw<InvalidOperationException>("a name collision would corrupt historical reads");
    }
}
