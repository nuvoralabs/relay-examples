using Microsoft.EntityFrameworkCore;
using Nuvora.Nexus.Relay.EventStore.EfCore.Persistence;

namespace Feed.Subscriptions;

/// <summary>
/// The host's DbContext. The relay event-log table and the durable named-subscription table are mapped
/// onto it by the ApplyRelay*() model-builder extensions, so a feed's events and the position it has
/// reached live side by side in the host's database. ApplyRelayEventStore() maps relay_events (the log a
/// subscription reads forward over); ApplyRelaySubscriptions() maps relay_subscriptions (one row per named
/// consumer, holding its last-acknowledged gap-safe cursor).
/// </summary>
public class FeedDbContext : DbContext
{
    public FeedDbContext(DbContextOptions<FeedDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyRelayEventStore();      // relay_events — the log a subscription advances over
        modelBuilder.ApplyRelaySubscriptions();   // relay_subscriptions — the durable per-name position
    }
}
