using FluentAssertions;
using Transport.Sample;
using Xunit;

namespace Transport.Sample.Tests;

public sealed class TransportTests
{
    [Fact]
    public async Task A_published_message_is_delivered_to_a_subscribed_consumer()
    {
        using var provider = TransportScenario.BuildProvider();

        var received = await TransportScenario.PublishAndConsumeAsync(provider, "order.placed");

        received.Should().ContainSingle().Which.Should().Be("order.placed");
    }

    [Fact]
    public async Task Every_published_message_is_delivered()
    {
        using var provider = TransportScenario.BuildProvider();

        var received = await TransportScenario.PublishAndConsumeAsync(provider, "order.placed", "order.shipped", "order.delivered");

        received.Should().BeEquivalentTo(new[] { "order.placed", "order.shipped", "order.delivered" });
    }
}
