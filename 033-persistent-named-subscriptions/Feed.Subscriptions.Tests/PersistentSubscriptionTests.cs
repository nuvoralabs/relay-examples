using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Nuvora.Nexus.Relay.EventStore;
using Xunit;

namespace Feed.Subscriptions;

/// <summary>
/// Proves the four properties that make a durable named subscription trustworthy as a resumable feed
/// position: it starts at the beginning, advancing persists, the position is monotonic, and — the point
/// of "durable" — a fresh process against the same database resumes from where the last one left off.
/// Each test uses a unique subscription name so the shared <c>relay_subscriptions</c> table never couples
/// tests. These are integration tests against real PostgreSQL because the guarantee being verified —
/// "the position survives the process" — only exists at the database level.
/// </summary>
[Collection("feed")]
public sealed class PersistentSubscriptionTests
{
    private readonly FeedFixture _fixture;

    public PersistentSubscriptionTests(FeedFixture fixture) => _fixture = fixture;

    private ISubscriptionStore Store => _fixture.Services.GetRequiredService<ISubscriptionStore>();

    private static string UniqueName() => "feed-" + Guid.NewGuid().ToString("N");

    [Fact]
    public async Task A_named_subscription_starts_at_the_beginning()
    {
        // An unknown consumer has no stored row, so it begins before the first event — a full replay.
        (await Store.GetPositionAsync(UniqueName())).Should().Be(EventCursor.Start);
        EventCursor.Start.Should().Be(new EventCursor(0, 0));
    }

    [Fact]
    public async Task Advancing_a_subscription_persists_its_position()
    {
        var store = Store;
        var name = UniqueName();

        // The consumer processed events up to (tx 10, position 100) and acknowledged them.
        await store.AdvanceAsync(name, new EventCursor(10, 100));

        (await store.GetPositionAsync(name)).Should().Be(new EventCursor(10, 100));
    }

    [Fact]
    public async Task The_position_is_monotonic_and_never_moves_backward()
    {
        var store = Store;
        var name = UniqueName();

        await store.AdvanceAsync(name, new EventCursor(10, 100));

        // A late or duplicated ack for an earlier place is ignored — the position cannot regress.
        await store.AdvanceAsync(name, new EventCursor(5, 50));
        (await store.GetPositionAsync(name)).Should().Be(new EventCursor(10, 100));

        // Acking the same place again is a no-op too (only strictly-forward moves take effect).
        await store.AdvanceAsync(name, new EventCursor(10, 100));
        (await store.GetPositionAsync(name)).Should().Be(new EventCursor(10, 100));

        // A genuine forward move (same tx, further position) is accepted.
        await store.AdvanceAsync(name, new EventCursor(10, 150));
        (await store.GetPositionAsync(name)).Should().Be(new EventCursor(10, 150));

        // And a later transaction advances it further still.
        await store.AdvanceAsync(name, new EventCursor(11, 160));
        (await store.GetPositionAsync(name)).Should().Be(new EventCursor(11, 160));
    }

    [Fact]
    public async Task A_fresh_process_resumes_from_the_stored_position_after_a_restart()
    {
        var name = UniqueName();
        var resumePoint = new EventCursor(42, 4200);

        // Process #1: the consumer makes progress and acknowledges it, then "exits" (we dispose its
        // provider — every connection, cache and the store singleton go away).
        var processOne = FeedFixture.BuildProvider(_fixture.ConnectionString);
        await using (processOne)
        {
            var store = processOne.GetRequiredService<ISubscriptionStore>();
            await store.AdvanceAsync(name, resumePoint);
            (await store.GetPositionAsync(name)).Should().Be(resumePoint);
        }

        // Process #2: a brand-new ServiceProvider against the SAME database — nothing shared in memory
        // with process #1. It reads the position that was persisted and so resumes exactly where the
        // first process stopped, rather than replaying the whole feed.
        var processTwo = FeedFixture.BuildProvider(_fixture.ConnectionString);
        await using (processTwo)
        {
            var store = processTwo.GetRequiredService<ISubscriptionStore>();

            (await store.GetPositionAsync(name)).Should().Be(resumePoint,
                "a durable subscription's position outlives the process that recorded it");

            // And it keeps advancing forward from there — the restart was seamless.
            await store.AdvanceAsync(name, new EventCursor(43, 4300));
            (await store.GetPositionAsync(name)).Should().Be(new EventCursor(43, 4300));
        }
    }
}
