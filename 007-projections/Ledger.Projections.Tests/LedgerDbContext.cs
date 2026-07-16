using Ledger.Projections.ReadModels;
using Microsoft.EntityFrameworkCore;
using Nuvora.Nexus.Relay.EventStore.EfCore.Persistence;
using Nuvora.Nexus.Relay.Projections.EfCore.Persistence;

namespace Ledger.Projections;

public class LedgerDbContext : DbContext
{
    public LedgerDbContext(DbContextOptions<LedgerDbContext> options) : base(options)
    {
    }

    public DbSet<AccountBalanceReadModel> AccountBalances => Set<AccountBalanceReadModel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyRelayEventStore();
        modelBuilder.ApplyRelaySnapshots();
        // Maps relay_projection_checkpoints (+ dead-letters + failures) so projection progress is durable.
        modelBuilder.ApplyRelayProjectionCheckpoints();

        modelBuilder.Entity<AccountBalanceReadModel>(e =>
        {
            e.ToTable("account_balances");
            e.HasKey(r => r.Id);
        });
    }
}
