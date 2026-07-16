using Microsoft.EntityFrameworkCore;
using Nuvora.Nexus.Relay.EventStore.EfCore.Persistence;

namespace Ordering.Metadata;

/// <summary>
/// The host's DbContext. The relay event-store tables are mapped onto it by the ApplyRelay*() model
/// builder extensions, so the events — and the enriched metadata stored alongside each one — live in the
/// host's database and commit in the host's transaction. ApplyRelaySnapshots() is required even though
/// this sample takes no snapshots: the event-sourced repository checks for a snapshot on every load.
/// </summary>
public class OrderingDbContext : DbContext
{
    public OrderingDbContext(DbContextOptions<OrderingDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyRelayEventStore();
        modelBuilder.ApplyRelaySnapshots();
    }
}
