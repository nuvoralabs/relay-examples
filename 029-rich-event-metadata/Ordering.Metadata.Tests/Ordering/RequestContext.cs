namespace Ordering.Metadata.Ordering;

/// <summary>
/// The ambient, per-command facts an enricher stamps onto every event. In a real service these come
/// from inbound HTTP middleware / the message envelope (a correlation id from the caller, the
/// authenticated user, the resolved tenant) and are placed here once, at the edge of the request. It is
/// registered <b>scoped</b> so it lives exactly as long as one command's scope — the same scope the
/// transactional pipeline resolves the enricher from, so whatever the handler put here is visible when
/// the events are appended.
/// </summary>
public sealed class RequestContext
{
    /// <summary>The end-to-end id that ties together every event/message produced by one logical flow.</summary>
    public string? CorrelationId { get; set; }

    /// <summary>The id of the message/command that directly caused this work (for causation tracing).</summary>
    public string? CausationId { get; set; }

    /// <summary>Who initiated the command (user id, service principal, …) — the audit "who".</summary>
    public string? ActorId { get; set; }

    /// <summary>The tenant the command was issued for, in a multi-tenant deployment.</summary>
    public string? TenantId { get; set; }
}
