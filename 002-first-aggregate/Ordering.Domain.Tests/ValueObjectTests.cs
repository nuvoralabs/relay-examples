using FluentAssertions;
using Nuvora.Nexus.Relay.Core.Domain;
using Ordering.Domain;
using Xunit;

namespace Ordering.Domain.Tests;

public sealed class MoneyTests
{
    [Fact]
    public void Equal_when_amount_and_currency_match()
    {
        new Money(10m, "USD").Should().Be(new Money(10m, "usd")); // currency normalised to upper
        (new Money(10m, "USD") == new Money(10m, "USD")).Should().BeTrue();
    }

    [Fact]
    public void Not_equal_when_amount_or_currency_differ()
    {
        new Money(10m, "USD").Should().NotBe(new Money(11m, "USD"));
        new Money(10m, "USD").Should().NotBe(new Money(10m, "EUR"));
    }

    [Fact]
    public void Rounds_to_two_decimal_places()
    {
        new Money(10.1234m, "USD").Amount.Should().Be(10.12m);
    }

    [Fact]
    public void Add_sums_amounts_in_the_same_currency()
    {
        new Money(10m, "USD").Add(new Money(2.50m, "USD")).Should().Be(new Money(12.50m, "USD"));
    }

    [Fact]
    public void Add_across_currencies_is_a_domain_error()
    {
        var act = () => new Money(10m, "USD").Add(new Money(1m, "EUR"));
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Negative_amount_is_a_domain_error()
    {
        var act = () => new Money(-1m, "USD");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Invalid_currency_is_a_domain_error()
    {
        var act = () => new Money(1m, "DOLLARS");
        act.Should().Throw<DomainException>();
    }
}

public sealed class SkuTests
{
    [Fact]
    public void Comparison_is_case_insensitive()
    {
        new Sku("abc-1").Should().Be(new Sku("ABC-1"));
    }

    [Theory]
    [InlineData("ab")]                                   // too short
    [InlineData("this-sku-is-far-too-long-to-be-valid-xx")] // too long
    [InlineData("   ")]                                  // whitespace
    public void Rejects_invalid_values(string value)
    {
        var act = () => new Sku(value);
        act.Should().Throw<System.Exception>(); // ArgumentException for whitespace, DomainException for length
    }
}
