using Nuvora.Nexus.Relay.Core.Domain;

namespace Ordering.Domain;

/// <summary>
/// A value object: an amount in a currency. Value objects have no identity — two <see cref="Money"/>
/// instances are equal when their components are equal — and they are immutable, so "changing" one
/// returns a new instance. Invariants are enforced in the constructor with <see cref="Guard"/>, so an
/// invalid <see cref="Money"/> can never exist.
/// </summary>
public sealed class Money : ValueObject
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        Guard.AgainstNullOrWhiteSpace(currency, nameof(currency));
        Guard.Against(currency.Trim().Length != 3, "Currency must be a 3-letter ISO code.");
        Guard.AgainstNegative(amount, nameof(amount));

        Amount = decimal.Round(amount, 2, MidpointRounding.ToEven);
        Currency = currency.Trim().ToUpperInvariant();
    }

    public static Money Zero(string currency) => new(0m, currency);

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Multiply(int factor)
    {
        Guard.AgainstNegative(factor, nameof(factor));
        return new Money(Amount * factor, Currency);
    }

    private void EnsureSameCurrency(Money other)
    {
        Guard.AgainstNull(other, nameof(other));
        Guard.Against(
            other.Currency != Currency,
            $"Cannot combine amounts in different currencies ({Currency} and {other.Currency}).");
    }

    // The single source of truth for equality and hashing — list every field that defines the value.
    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }

    public override string ToString() => $"{Amount:0.00} {Currency}";
}
