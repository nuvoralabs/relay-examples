using Microsoft.EntityFrameworkCore;
using Nuvora.Nexus.Relay.EventStore.EfCore.Persistence;

namespace Ledger.Archiving;

/// <summary>
/// The host's DbContext. The relay event-store tables are mapped onto it by the ApplyRelay*() model
/// builder extensions, so the live log (<c>relay_events</c>) and the compaction target
/// (<c>relay_events_archive</c>) live in the host's database. <c>ApplyRelayEventStore()</c> maps the hot
/// table; <c>ApplyRelayEventArchive()</c> maps the archive — both are required for the archiver to run.
/// <c>ApplyRelaySnapshots()</c> is mapped because the event-sourced repository always probes for a
/// snapshot on load (article 006), and in production the snapshot is what makes archiving safe: replay
/// starts after the snapshot, so an archived prefix never has to be read.
/// </summary>
public class ArchiveDbContext : DbContext
{
    public ArchiveDbContext(DbContextOptions<ArchiveDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyRelayEventStore();     // relay_events (the hot, live log)
        modelBuilder.ApplyRelaySnapshots();      // relay_snapshots (probed on every load)
        modelBuilder.ApplyRelayEventArchive();   // relay_events_archive (the compaction target)
    }
}
