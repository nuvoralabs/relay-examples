# Sample 019 — Recurring Jobs

Companion to **[Article 019 — Recurring Jobs](../../docs/articles/019-recurring-jobs.md)**.

Two pieces of "do work on a schedule":

- **Cron planning** — `CronRecurringOccurrencePlanner` (pure, no infrastructure): computes the next run
  of a cron schedule in a time zone, and on catch-up which missed occurrences to enqueue
  (`Skip` vs `Backfill`).
- **Jobs** — `JobScheduler.EnqueueAsync(job)` stages a durable `IJob`; the `JobScheduledMessageDispatcher`
  resolves and invokes the registered `IJobHandler<TJob>`.

DB-free unit tests.

## Run it

```bash
dotnet test samples/019-recurring-jobs/Recurring.Sample.Tests
```

> Requires the **.NET 10 SDK**. No Docker/database needed. In production, `AddRelayRecurringScheduling()`
> + `AddRelayRecurringSchedulingEfCore<TContext>()` persist cron schedules and a processor enqueues due
> occurrences; `AddRelayJobs()` + `AddJobHandler<TJob, THandler>()` wire the job side.
