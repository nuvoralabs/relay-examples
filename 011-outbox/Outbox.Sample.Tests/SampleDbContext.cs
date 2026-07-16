using Microsoft.EntityFrameworkCore;
using Nuvora.Nexus.Relay.EventStore.EfCore.Persistence;
using Nuvora.Nexus.Relay.Outbox.EfCore.Persistence;

namespace Outbox.Sample;

public class SampleDbContext : DbContext
{
    public SampleDbContext(DbContextOptions<SampleDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyRelayEventStore();
        modelBuilder.ApplyRelaySnapshots();
        modelBuilder.ApplyRelayOutbox(); // the relay_outbox_messages table
    }
}
