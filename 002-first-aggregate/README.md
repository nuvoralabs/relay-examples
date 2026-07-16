# Sample 002 — First Aggregate (Ordering domain)

Companion to **[Article 002 — First Aggregate](https://relay.nuvoralabs.com/articles/first-aggregate/)**.

A pure domain model — no web host, no database, no infrastructure — that demonstrates Relay's DDD
building blocks:

- **`AggregateRoot<Guid>`** (`Order`) as a consistency boundary whose every change goes through a
  command method that checks invariants and then raises a **domain event**.
- **`Entity<Guid>`** (`OrderLine`) — an identity-bearing object that lives inside the aggregate.
- **`ValueObject`** (`Money`, `Sku`) — immutable, equal-by-value, self-validating.
- **`Guard`** clauses for invariants (domain-rule violations throw `DomainException`; bad arguments
  throw `ArgumentException`).
- **`DomainEvent`** records, and the **replay-safety** that comes from mutating state only in
  `ApplyEvent` (`Order.FromHistory` rebuilds an order purely from its events). This is the bridge to
  event sourcing in article 005.

## Layout

```
Ordering.Domain/
  Money.cs        # value object (amount + currency) with arithmetic + invariants
  Sku.cs          # value object wrapping a validated, normalised SKU string
  OrderLine.cs    # entity inside the aggregate
  OrderEvents.cs  # OrderPlaced / OrderLineAdded / ... domain events
  Order.cs        # the aggregate root + the apply-only state machine
Ordering.Domain.Tests/
  ValueObjectTests.cs  # Money + Sku equality and invariants
  OrderTests.cs        # aggregate behaviour, invariants, and replay equivalence
```

## Test it

```bash
dotnet test samples/002-first-aggregate/Ordering.Domain.Tests
```

> Requires the **.NET 10 SDK**. No Docker/database/web host needed — these are fast, in-memory unit
> tests, which is exactly the point of keeping the domain pure.

## Try it in a REPL

```csharp
var order = Ordering.Domain.Order.Place(Guid.NewGuid(), "cust-42", "USD");
order.AddLine("SKU-CHAIR", 2, new Ordering.Domain.Money(100m, "USD"));
order.AddLine("SKU-DESK", 1, new Ordering.Domain.Money(250m, "USD"));
order.Confirm();
Console.WriteLine(order.Total);            // 450.00 USD
Console.WriteLine(order.GetUncommittedChanges().Count); // 4 events ready to persist
```
