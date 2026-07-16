using Microsoft.EntityFrameworkCore;
using Nuvora.Nexus.Relay.EventStore.EfCore.Persistence;
using Nuvora.Nexus.Relay.Inbox.EfCore.Persistence;

namespace Inbox.Sample;

public class SampleDbContext : DbContext
{
    public SampleDbContext(DbContextOptions<SampleDbContext> options) : base(options)
    {
    }

    public DbSet<HandledOrder> HandledOrders => Set<HandledOrder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyRelayEventStore();
        modelBuilder.ApplyRelaySnapshots();
        modelBuilder.ApplyRelayInbox(); // the relay_inbox_messages dedup table

        modelBuilder.Entity<HandledOrder>(e =>
        {
            e.ToTable("handled_orders");
            e.HasKey(h => h.Id);
        });
    }
}
