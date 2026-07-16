using Microsoft.EntityFrameworkCore;
using Nuvora.Nexus.Relay.EventStore.EfCore.Persistence;
using Nuvora.Nexus.Relay.Outbox.EfCore.Persistence;
using Nuvora.Nexus.Relay.Projections.EfCore.Persistence;

namespace Storefront.Sample;

/// <summary>
/// One DbContext for the whole service: the event store (write model), the outbox, the projection
/// checkpoints, and the <see cref="OrderSummary"/> read model. Because they share a context, a command's
/// event append, its outbox row, and (separately) a projection's read-model write each commit atomically
/// with their checkpoint — no dual writes anywhere.
/// </summary>
public class StorefrontDbContext : DbContext
{
    public StorefrontDbContext(DbContextOptions<StorefrontDbContext> options) : base(options)
    {
    }

    public DbSet<OrderSummary> OrderSummaries => Set<OrderSummary>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyRelayEventStore();            // relay_events (the source of truth)
        modelBuilder.ApplyRelaySnapshots();
        modelBuilder.ApplyRelayOutbox();                // relay_outbox_messages (integration events)
        modelBuilder.ApplyRelayProjectionCheckpoints(); // checkpoints + dead-letters + failures

        modelBuilder.Entity<OrderSummary>(e =>
        {
            e.ToTable("order_summaries");
            e.HasKey(r => r.OrderId);
        });
    }
}
