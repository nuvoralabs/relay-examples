using Nuvora.Nexus.Relay.EventStore;

namespace Ordering.Metadata.Ordering;

/// <summary>
/// The custom <see cref="IEventMetadataEnricher"/>. Registering it in DI is all it takes for Relay to
/// call it on every command's event append: the <c>TransactionExecutor</c> resolves every registered
/// enricher (<c>GetServices&lt;IEventMetadataEnricher&gt;()</c>) and runs <see cref="EventMetadata.Build"/>,
/// which lets each one contribute entries to the per-event metadata bag before serializing it to JSON.
///
/// <para>It reads the scoped <see cref="RequestContext"/> — the enricher is resolved from the command's
/// own scope, so it sees the same context the handler populated. It only writes keys that are present
/// (so an absent header simply isn't stamped) and never overwrites the framework's authoritative
/// <c>CommandName</c>/<c>Timestamp</c> keys (those are written last by <see cref="EventMetadata.Build"/>).</para>
/// </summary>
public sealed class RequestContextEnricher(RequestContext context) : IEventMetadataEnricher
{
    // Stable header names. Keep them small and primitive — metadata is stored on EVERY event.
    public const string CorrelationIdKey = "CorrelationId";
    public const string CausationIdKey = "CausationId";
    public const string ActorIdKey = "ActorId";
    public const string TenantIdKey = "TenantId";

    public void Enrich(IDictionary<string, object?> metadata, string commandName)
    {
        if (context.CorrelationId is { } correlationId)
        {
            metadata[CorrelationIdKey] = correlationId;
        }

        if (context.CausationId is { } causationId)
        {
            metadata[CausationIdKey] = causationId;
        }

        if (context.ActorId is { } actorId)
        {
            metadata[ActorIdKey] = actorId;
        }

        if (context.TenantId is { } tenantId)
        {
            metadata[TenantIdKey] = tenantId;
        }
    }
}
