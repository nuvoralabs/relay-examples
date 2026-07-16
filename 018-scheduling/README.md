# Sample 018 — Scheduling

Companion to **[Article 018 — Scheduling](../../docs/articles/018-scheduling.md)**.

Run a command **later**. `IScheduler.ScheduleCommandAsync(command, delay)` stages a durable scheduled
message with the right due time; when due, the `SchedulerProcessor` claims it and the
`CommandScheduledMessageDispatcher` deserialises and executes the command through the `ICommandBus`. This
sample uses an in-memory scheduler repository (no database) and drives the claim/dispatch by hand.

## Run it

```bash
dotnet test samples/018-scheduling/Scheduling.Sample.Tests
```

> Requires the **.NET 10 SDK**. No Docker/database needed. In production, `AddRelayScheduling()` +
> `AddRelaySchedulerEfCore<TContext>()` store scheduled messages in PostgreSQL and the
> `SchedulerProcessor` claims due rows with `FOR UPDATE SKIP LOCKED`.
