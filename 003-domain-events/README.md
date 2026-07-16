# Sample 003 — Domain Events

Companion to **[Article 003 — Domain Events](https://relay.nuvoralabs.com/articles/domain-events/)**.

A small **console** app (no web host, no database) showing how a domain event raised by an aggregate
fans out to multiple **domain-event handlers** that react independently:

- The `Ticket` aggregate raises `TicketOpened` / `TicketAssigned` / `TicketClosed`.
- `TicketReadModelProjector` (a single handler implementing three `IDomainEventHandler<T>` interfaces)
  keeps a read model in sync.
- `HighPriorityTicketAlerter` is a **second, independent** handler for `TicketOpened` — proof that
  events fan out: two reactors, neither aware of the other or of the aggregate.
- Command handlers publish the aggregate's uncommitted events via `IDomainEventBus`.

> The command handlers publish events explicitly **only because this sample has no persistence stack**.
> In a real event-sourced service (article 005) the transactional pipeline dispatches an aggregate's
> domain events automatically, inside the transaction that saves them. This sample makes the dispatch
> visible with zero infrastructure.

## Layout

```
Fulfillment/
  Tickets/Ticket.cs              # the aggregate
  Tickets/TicketEvents.cs        # TicketOpened / TicketAssigned / TicketClosed
  Tickets/Stores.cs              # in-memory aggregate store, read model, alert log
  Tickets/DomainEventHandlers.cs # the projector + the alerter (IDomainEventHandler<T>)
  Tickets/Commands.cs            # commands + handlers that publish via IDomainEventBus
  Tickets/Queries.cs             # reads the projected view
  FulfillmentServiceCollectionExtensions.cs # AddFulfillment(): stores + AddRelay
  Program.cs                     # builds the container and runs a scenario
Fulfillment.Tests/
  TicketFlowTests.cs             # dispatches commands, asserts handlers updated the read model + alerts
```

## Run it

```bash
dotnet run --project samples/003-domain-events/Fulfillment
```

Expected output:

```
Ticket "Cannot log in" is now Closed, assigned to agent-amy.
ALERT: High-priority ticket opened: "Cannot log in" (correlation 8f3c…)
```

## Test it

```bash
dotnet test samples/003-domain-events/Fulfillment.Tests
```

> Requires the **.NET 10 SDK**. No Docker/database needed.
