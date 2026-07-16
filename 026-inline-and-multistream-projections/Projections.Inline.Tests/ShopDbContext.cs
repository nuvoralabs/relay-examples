using Microsoft.EntityFrameworkCore;
using Projections.Inline.ReadModels;
using Nuvora.Nexus.Relay.EventStore.EfCore.Persistence;

namespace Projections.Inline;

/// <summary>
/// The host's DbContext. The relay event-store tables are mapped on by ApplyRelayEventStore(), so the
/// events live in the host's database and commit in the host's transaction — and so do the two read-model
/// tables mapped below, which is what makes an inline projection's write atomic with the events.
/// ApplyRelaySnapshots() is required even though this sample takes no snapshots (the repository always
/// checks for one on load). No ApplyRelayProjectionCheckpoints() here: the inline path needs no checkpoint
/// (it runs in the commit), and the multi-stream demo drives the projection directly rather than via the
/// async host.
/// </summary>
public class ShopDbContext : DbContext
{
    public ShopDbContext(DbContextOptions<ShopDbContext> options) : base(options)
    {
    }

    public DbSet<OrderSummaryReadModel> OrderSummaries => Set<OrderSummaryReadModel>();
    public DbSet<FulfillmentReadModel> Fulfillments => Set<FulfillmentReadModel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyRelayEventStore();
        modelBuilder.ApplyRelaySnapshots();

        modelBuilder.Entity<OrderSummaryReadModel>(e =>
        {
            e.ToTable("order_summaries");
            e.HasKey(r => r.OrderId);
        });

        modelBuilder.Entity<FulfillmentReadModel>(e =>
        {
            e.ToTable("fulfillments");
            e.HasKey(r => r.OrderId);
        });
    }
}
