using Microsoft.EntityFrameworkCore;
using Nuvora.Nexus.Relay.EventStore.EfCore.Persistence;

namespace Coordination.Sample;

/// <summary>
/// Postgres advisory locks need no tables of their own, but <c>AddRelayEventStoreEfCore</c> wires the
/// lock to a <see cref="DbContext"/>, so we map the relay event-store tables and create them with
/// <c>EnsureCreated</c>. A real service would reuse its existing event-store context.
/// </summary>
public class CoordinationDbContext : DbContext
{
    public CoordinationDbContext(DbContextOptions<CoordinationDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyRelayEventStore();
        modelBuilder.ApplyRelaySnapshots();
    }
}
