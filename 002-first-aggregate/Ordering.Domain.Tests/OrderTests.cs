using FluentAssertions;
using Nuvora.Nexus.Relay.Core.Domain;
using Ordering.Domain;
using Xunit;

namespace Ordering.Domain.Tests;

public sealed class OrderTests
{
    private static Order ADraftWithOneLine(out Guid id)
    {
        id = Guid.NewGuid();
        var order = Order.Place(id, "cust-1", "USD");
        order.AddLine("SKU-CHAIR", 2, new Money(100m, "USD"));
        return order;
    }

    [Fact]
    public void Place_creates_an_empty_draft_and_raises_OrderPlaced()
    {
        var id = Guid.NewGuid();

        var order = Order.Place(id, "cust-1", "USD");

        order.Id.Should().Be(id);
        order.Status.Should().Be(OrderStatus.Draft);
        order.Lines.Should().BeEmpty();
        order.Total.Should().Be(Money.Zero("USD"));

        // No events are committed yet: Version is -1 (a brand-new stream) and the OrderPlaced event
        // is sitting in the uncommitted list waiting to be persisted.
        order.Version.Should().Be(-1);
        order.GetUncommittedChanges().Should().ContainSingle().Which.Should().BeOfType<OrderPlaced>();
    }

    [Fact]
    public void Place_rejects_an_empty_id_as_a_domain_error()
    {
        var act = () => Order.Place(Guid.Empty, "cust-1", "USD");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Place_rejects_a_blank_customer_as_an_argument_error()
    {
        var act = () => Order.Place(Guid.NewGuid(), "  ", "USD");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddLine_adds_a_line_and_updates_the_total()
    {
        var order = ADraftWithOneLine(out _);

        order.AddLine("SKU-DESK", 1, new Money(250m, "USD"));

        order.Lines.Should().HaveCount(2);
        order.Total.Should().Be(new Money(450m, "USD")); // 2×100 + 1×250
    }

    [Fact]
    public void AddLine_rejects_a_duplicate_sku()
    {
        var order = ADraftWithOneLine(out _);

        var act = () => order.AddLine("sku-chair", 1, new Money(100m, "USD")); // same SKU, different case
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void AddLine_rejects_a_currency_that_does_not_match_the_order()
    {
        var order = ADraftWithOneLine(out _);

        var act = () => order.AddLine("SKU-LAMP", 1, new Money(20m, "EUR"));
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void AddLine_rejects_a_non_positive_quantity()
    {
        var order = ADraftWithOneLine(out _);

        var act = () => order.AddLine("SKU-LAMP", 0, new Money(20m, "USD"));
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Confirm_requires_at_least_one_line()
    {
        var order = Order.Place(Guid.NewGuid(), "cust-1", "USD");

        var act = () => order.Confirm();
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Confirm_transitions_a_draft_to_confirmed()
    {
        var order = ADraftWithOneLine(out _);

        order.Confirm();

        order.Status.Should().Be(OrderStatus.Confirmed);
        order.GetUncommittedChanges().Last().Should().BeOfType<OrderConfirmed>();
    }

    [Fact]
    public void A_confirmed_order_is_immutable()
    {
        var order = ADraftWithOneLine(out _);
        order.Confirm();

        var act = () => order.AddLine("SKU-DESK", 1, new Money(250m, "USD"));
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Cancelling_twice_is_a_domain_error()
    {
        var order = ADraftWithOneLine(out _);
        order.Cancel("changed mind");

        var act = () => order.Cancel("again");
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void MarkChangesAsCommitted_advances_the_version_and_clears_events()
    {
        var order = Order.Place(Guid.NewGuid(), "cust-1", "USD"); // one event, version -1

        order.MarkChangesAsCommitted();

        order.Version.Should().Be(0); // -1 + 1 committed event
        order.GetUncommittedChanges().Should().BeEmpty();
    }

    [Fact]
    public void Uncommitted_events_are_recorded_in_order_with_contiguous_versions()
    {
        var order = ADraftWithOneLine(out _);
        order.AddLine("SKU-DESK", 1, new Money(250m, "USD"));
        order.Confirm();

        var events = order.GetUncommittedChanges().ToList();

        events.Select(e => e.GetType()).Should().Equal(
            typeof(OrderPlaced), typeof(OrderLineAdded), typeof(OrderLineAdded), typeof(OrderConfirmed));
        events.Select(e => e.Version).Should().Equal(0, 1, 2, 3);
    }

    [Fact]
    public void An_order_rebuilt_from_history_is_identical_to_the_original()
    {
        var original = ADraftWithOneLine(out var id);
        original.AddLine("SKU-DESK", 1, new Money(250m, "USD"));
        original.Confirm();

        // The events are the single source of truth — replay them and the state comes back exactly.
        var history = original.GetUncommittedChanges().ToList();
        var rebuilt = Order.FromHistory(history);

        rebuilt.Id.Should().Be(id);
        rebuilt.Status.Should().Be(OrderStatus.Confirmed);
        rebuilt.Lines.Should().HaveCount(2);
        rebuilt.Total.Should().Be(original.Total);
        rebuilt.Version.Should().Be(3); // version of the last replayed event
        rebuilt.GetUncommittedChanges().Should().BeEmpty(); // replay does not raise new events
    }
}
