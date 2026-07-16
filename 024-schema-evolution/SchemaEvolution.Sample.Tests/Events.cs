using Newtonsoft.Json.Linq;
using Nuvora.Nexus.Relay.Core.Domain;
using Nuvora.Nexus.Relay.EventStore;

namespace SchemaEvolution.Sample;

// The CURRENT shape of the event. Earlier versions called the field "LegacyAmount"; an upcaster
// (below) rewrites old payloads into this shape on read, so historical events keep deserializing.
public sealed record InvoiceRaised(Guid InvoiceId, decimal Amount) : DomainEvent
{
    public override Guid AggregateId => InvoiceId;
}

// [EventType] pins a STABLE persisted name, decoupled from the CLR type name. Rename or move the class
// and stored events still resolve — without it, the CLR FullName is persisted and a rename orphans
// every historical event.
[EventType("samples.order-shipped")]
public sealed record OrderShipped(Guid OrderId) : DomainEvent
{
    public override Guid AggregateId => OrderId;
}

/// <summary>
/// Upcasts the v1 <c>InvoiceRaised</c> payload (persisted under the name <c>samples.invoice-raised.v1</c>
/// with a <c>LegacyAmount</c> field) to the current shape: renames the field to <c>Amount</c> and maps
/// the type name forward to the current <see cref="InvoiceRaised"/>. The serializer runs the chain on
/// read before deserialization, so the rest of the system only ever sees the current type.
/// </summary>
public sealed class InvoiceRaisedV1Upcaster : IEventUpcaster
{
    public const string V1TypeName = "samples.invoice-raised.v1";

    public bool CanUpcast(string typeName) => typeName == V1TypeName;

    public (string TypeName, JObject Json) Upcast(string typeName, JObject json)
    {
        json["Amount"] = json["LegacyAmount"];
        json.Remove("LegacyAmount");
        return (typeof(InvoiceRaised).FullName!, json);
    }
}
