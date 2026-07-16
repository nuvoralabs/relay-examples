using FluentAssertions;
using Nuvora.Nexus.Relay.EventStore;
using Xunit;

namespace Forensics.EventQueries;

/// <summary>
/// Demonstrates the default <see cref="IEventStore.QueryEventsAsync"/> as an operational/forensic tool:
/// a paged predicate scan over the global event log that returns matches in global-position order,
/// capped at <c>maxResults</c>. This is for incident investigation — it is <b>not</b> a query engine
/// (projections are; see article 007).
/// <para>
/// CRITICAL: <see cref="IEventStore.QueryEventsAsync"/> is a <b>default interface method</b>, so it is
/// callable only through an <see cref="IEventStore"/>-typed reference — never through the concrete
/// <see cref="InMemoryEventStore"/>. The <see cref="StoreWith"/> helper returns the store typed as
/// <see cref="IEventStore"/> for exactly this reason.
/// </para>
/// </summary>
public class OperationalEventQueryTests
{
    private static EventData Evt(long position, string eventType)
        => new(Guid.NewGuid(), "stream", eventType, Guid.NewGuid(), "Agg", 0, position, DateTimeOffset.UtcNow, "{}");

    // Returns IEventStore (not the concrete InMemoryEventStore): QueryEventsAsync is a default interface
    // method, so it is only callable through the interface type. Calling it on the concrete class would
    // not compile.
    private static IEventStore StoreWith(params (long Position, string Type)[] events)
    {
        var store = new InMemoryEventStore();
        foreach (var (position, type) in events)
        {
            store.Events.Add(Evt(position, type));
        }

        return store;
    }

    [Fact]
    public async Task Returns_matches_in_global_position_order()
    {
        // Seeded out of order; the scan must still hand them back by global position.
        var store = StoreWith((5, "PaymentFailed"), (1, "PaymentFailed"), (3, "OrderPlaced"), (2, "PaymentFailed"), (4, "OrderPlaced"));

        var result = await store.QueryEventsAsync(e => e.EventType == "PaymentFailed");

        result.Select(e => e.Position).Should().Equal(1, 2, 5);
    }

    [Fact]
    public async Task Honours_max_results()
    {
        var store = StoreWith((1, "PaymentFailed"), (2, "PaymentFailed"), (3, "PaymentFailed"), (4, "PaymentFailed"));

        var result = await store.QueryEventsAsync(e => e.EventType == "PaymentFailed", maxResults: 2);

        result.Should().HaveCount(2);
        result.Select(e => e.Position).Should().Equal(1, 2);
    }

    [Fact]
    public async Task Empty_when_nothing_matches()
    {
        var store = StoreWith((1, "OrderPlaced"), (2, "PaymentSucceeded"));

        (await store.QueryEventsAsync(e => e.EventType == "PaymentFailed")).Should().BeEmpty();
    }

    [Fact]
    public async Task Guards_a_null_predicate()
    {
        var store = StoreWith((1, "OrderPlaced"));

        Func<Task> act = () => store.QueryEventsAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Guards_a_non_positive_max_results(int maxResults)
    {
        var store = StoreWith((1, "OrderPlaced"));

        Func<Task> act = () => store.QueryEventsAsync(_ => true, maxResults);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }
}
