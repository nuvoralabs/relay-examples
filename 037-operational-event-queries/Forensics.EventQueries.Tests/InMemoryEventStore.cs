using Nuvora.Nexus.Relay.EventStore;

namespace Forensics.EventQueries;

/// <summary>
/// A minimal in-memory <see cref="IEventStore"/> for the sample: it implements only the paged
/// <see cref="GetAllEventsAsync(long, int?, CancellationToken)"/> that the default
/// <see cref="IEventStore.QueryEventsAsync"/> builds on. Every other member throws
/// <see cref="NotSupportedException"/> — the forensic scan never touches them.
/// <para>
/// No database, broker, or container: the global log is just a <see cref="List{T}"/> we order by
/// <see cref="EventData.Position"/>. <c>fromPosition</c> is treated as <b>exclusive</b> (return only
/// events with <c>Position &gt; fromPosition</c>), which is exactly what the paged default requires to
/// make forward progress.
/// </para>
/// </summary>
public sealed class InMemoryEventStore : IEventStore
{
    public List<EventData> Events { get; } = new();

    public Task<IReadOnlyList<EventData>> GetAllEventsAsync(long fromPosition = 0, int? maxCount = null, CancellationToken cancellationToken = default)
    {
        IEnumerable<EventData> query = Events
            .Where(e => e.Position > fromPosition)
            .OrderBy(e => e.Position);

        if (maxCount.HasValue)
        {
            query = query.Take(maxCount.Value);
        }

        return Task.FromResult<IReadOnlyList<EventData>>(query.ToList());
    }

    // The forensic scan only needs the paged global read above; the rest of the contract is irrelevant
    // to this sample, so it is intentionally unsupported.
    public Task AppendAsync(EventData eventData, long? expectedVersion = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task AppendManyAsync(IEnumerable<EventData> events, long? expectedVersion = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<EventData>> GetEventsAsync(Guid aggregateId, long fromVersion = 0, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<EventData>> GetStreamEventsAsync(string streamName, long fromPosition = 0, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<EventData>> GetEventsByTypeAsync(string eventType, long fromPosition = 0, int? maxCount = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<long> GetAggregateVersionAsync(Guid aggregateId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<bool> AggregateExistsAsync(Guid aggregateId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<long> GetCurrentPositionAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
