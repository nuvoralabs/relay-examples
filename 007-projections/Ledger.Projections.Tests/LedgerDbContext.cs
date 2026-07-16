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

    public DbSet<VipAccountReadModel> VipAccounts => Set<VipAccountReadModel>();

    public DbSet<AccountActivityReadModel> AccountActivity => Set<AccountActivityReadModel>();

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

        modelBuilder.Entity<VipAccountReadModel>(e =>
        {
            e.ToTable("vip_accounts");
            e.HasKey(r => r.Id);
            e.HasIndex(r => r.IsVip); // the query shape: WHERE is_vip
        });

        modelBuilder.Entity<AccountActivityReadModel>(e =>
        {
            e.ToTable("account_monthly_activity");
            e.HasKey(r => new { r.AccountId, r.Year, r.Month });
            // The query shape: WHERE year = @y AND month = @m AND is_highly_active
            e.HasIndex(r => new { r.Year, r.Month, r.IsHighlyActive });
        });
    }
}
