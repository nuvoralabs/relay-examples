using System.Collections.Concurrent;

namespace Payments.Api.Payments;

/// <summary>The resource returned by the endpoints. The card is stored masked — never echo a PAN.</summary>
public sealed record Payment(string Id, decimal Amount, string Currency, string MaskedCard, string Status);

/// <summary>In-memory store (singleton). No database — this sample is about the error surface, not storage.</summary>
public sealed class PaymentStore
{
    private readonly ConcurrentDictionary<string, Payment> _payments = new();

    public Payment? Get(string id) => _payments.GetValueOrDefault(id);

    public Payment Save(Payment payment)
    {
        _payments[payment.Id] = payment;
        return payment;
    }
}

/// <summary>
/// A service-specific exception that is NOT part of the Relay exception hierarchy. It is mapped to an
/// HTTP status via <c>ExceptionHandlingOptions.CustomExceptionMappings</c> (see
/// PaymentsServiceCollectionExtensions) — the mechanism for giving your own exceptions a status code
/// without subclassing a framework type.
/// </summary>
public sealed class InsufficientFundsException(string message) : Exception(message);
