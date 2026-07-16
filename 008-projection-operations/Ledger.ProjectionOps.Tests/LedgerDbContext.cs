using Ledger.ProjectionOps.ReadModels;
using Microsoft.EntityFrameworkCore;
using Nuvora.Nexus.Relay.EventStore.EfCore.Persistence;
using Nuvora.Nexus.Relay.Projections.EfCore.Persistence;

namespace Ledger.ProjectionOps;

public class LedgerDbContext : DbContext
{
    public LedgerDbContext(DbContextOptions<LedgerDbContext> options) : base(options)
    {
    }

    public DbSet<AccountActivityReadModel> AccountActivity => Set<AccountActivityReadModel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyRelayEventStore();
        modelBuilder.ApplyRelaySnapshots();
        // checkpoints + dead-letters (where poison events land) + durable failure tallies
        modelBuilder.ApplyRelayProjectionCheckpoints();

        modelBuilder.Entity<AccountActivityReadModel>(e =>
        {
            e.ToTable("account_activity");
            e.HasKey(r => r.Id);
        });
    }
}
