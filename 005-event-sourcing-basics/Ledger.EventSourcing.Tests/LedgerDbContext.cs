using Microsoft.EntityFrameworkCore;
using Nuvora.Nexus.Relay.EventStore.EfCore.Persistence;

namespace Ledger.EventSourcing;

/// <summary>
/// The host's DbContext. The relay event-store tables are mapped onto it by the ApplyRelay*() model
/// builder extensions, so the events live in the host's database and commit in the host's transaction.
/// ApplyRelaySnapshots() is required even though this sample takes no snapshots — the repository always
/// checks for a snapshot on load (article 006 turns snapshots on).
/// </summary>
public class LedgerDbContext : DbContext
{
    public LedgerDbContext(DbContextOptions<LedgerDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyRelayEventStore();
        modelBuilder.ApplyRelaySnapshots();
    }
}
