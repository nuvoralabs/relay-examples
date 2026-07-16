using Microsoft.EntityFrameworkCore;
using Nuvora.Nexus.Relay.EventStore.EfCore.Persistence;

namespace Ledger.TimeTravel;

/// <summary>
/// The host's DbContext. The relay event-store tables are mapped onto it by the ApplyRelay*() model
/// builder extensions, so the events live in the host's database and commit in the host's transaction.
/// ApplyRelaySnapshots() is required even though this sample takes no snapshots — the write-side
/// repository checks for a snapshot on load. The reader deliberately ignores snapshots (a snapshot
/// captures the head, which would be wrong for a time-travel read), but the table must still be mapped.
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
