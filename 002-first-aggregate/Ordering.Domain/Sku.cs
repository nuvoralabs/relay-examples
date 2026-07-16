using Nuvora.Nexus.Relay.Core.Domain;

namespace Ordering.Domain;

/// <summary>
/// A value object wrapping a stock-keeping unit. Using a type instead of a bare <c>string</c> means
/// the validation lives in one place and an "SKU" can never be confused with any other string in a
/// method signature. Comparison is case-insensitive because the value is normalised on construction.
/// </summary>
public sealed class Sku : ValueObject
{
    public string Value { get; }

    public Sku(string value)
    {
        Guard.AgainstNullOrWhiteSpace(value, nameof(value));

        var normalized = value.Trim().ToUpperInvariant();
        Guard.Against(normalized.Length is < 3 or > 32, "A SKU must be between 3 and 32 characters.");

        Value = normalized;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
