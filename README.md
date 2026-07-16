# Relay Samples

Runnable, test-backed companion projects for the [Relay documentation](https://relay.nuvoralabs.dev/articles).
Each sample maps 1:1 to a numbered article and demonstrates exactly the concepts that article
teaches.

> New here, or want the *why* before the *how*? Start with the
> [**Relay in Stories**](https://relay.nuvoralabs.dev/stories) series — ten short, friendly tales of the
> business problems these patterns solve, each linking back to the tutorials below.

| Sample | Article | What it shows | Needs a database? |
|---|---|---|---|
| [`001-getting-started`](./001-getting-started) | [001 — Getting Started](https://relay.nuvoralabs.dev/articles/getting-started) | Commands, queries, handlers, a validator, the pipeline, attribute-routed HTTP endpoints, `ProblemDetails` | No (in-memory) |
| [`002-first-aggregate`](./002-first-aggregate) | [002 — First Aggregate](https://relay.nuvoralabs.dev/articles/first-aggregate) | `AggregateRoot`, entities, value objects, `Guard` invariants, domain events, replay-safety | No (pure domain) |
| [`003-domain-events`](./003-domain-events) | [003 — Domain Events](https://relay.nuvoralabs.dev/articles/domain-events) | `IDomainEventHandler<T>`, `IDomainEventBus`, fan-out, read-model projection, correlation | No (console) |
| [`004-errors-and-http`](./004-errors-and-http) | [004 — Errors & HTTP](https://relay.nuvoralabs.dev/articles/errors-and-http) | exception→`ProblemDetails` mapping, custom mappings, validation 400s, correlation/`traceId` | No (in-memory) |
| [`005-event-sourcing-basics`](./005-event-sourcing-basics) | [005 — Event Sourcing Basics](https://relay.nuvoralabs.dev/articles/event-sourcing-basics) | `IEventStore`, the transactional pipeline, `IEventSourcedRepository`, append/replay | **Yes** (Postgres/Docker) |
| [`006-concurrency-and-snapshots`](./006-concurrency-and-snapshots) | [006 — Concurrency & Snapshots](https://relay.nuvoralabs.dev/articles/concurrency-and-snapshots) | `ConcurrencyConflictException`, `ISnapshotable`, `SnapshotEvery`, bounded replay | **Yes** (Postgres/Docker) |
| [`007-projections`](./007-projections) | [007 — Projections](https://relay.nuvoralabs.dev/articles/projections) | `IProjection`, checkpoints, the catch-up host, read models | **Yes** (Postgres/Docker) |
| [`008-projection-operations`](./008-projection-operations) | [008 — Projection Operations](https://relay.nuvoralabs.dev/articles/projection-operations) | poison/dead-letter handling, rebuild from history (`ProjectionManager.ResetAsync`) | **Yes** (Postgres/Docker) |
| [`009-caching-queries`](./009-caching-queries) | [009 — Caching Queries](https://relay.nuvoralabs.dev/articles/caching-queries) | `[Cacheable]`, `IQueryCacheInvalidator`, deterministic hit/miss test | No (console) |
| [`010-authorization`](./010-authorization) | [010 — Authorization](https://relay.nuvoralabs.dev/articles/authorization) | `[RequireRole]`/`[RequirePermission]`/`[AllowAnonymous]`, fail-closed, 401/403 | No (in-memory) |
| [`011-outbox`](./011-outbox) | [011 — The Outbox Pattern](https://relay.nuvoralabs.dev/articles/outbox) | atomic event staging (no dual-write), rollback, fail-loud | **Yes** (Postgres/Docker) |
| [`012-messaging-and-transports`](./012-messaging-and-transports) | [012 — Messaging & Transports](https://relay.nuvoralabs.dev/articles/messaging-and-transports) | `IMessageBroker`/`IMessageConsumer`, in-memory transport, deterministic delivery | No (console) |
| [`013-inbox`](./013-inbox) | [013 — The Inbox Pattern](https://relay.nuvoralabs.dev/articles/inbox) | idempotent consumption, atomic dedup, redelivery retry | **Yes** (Postgres/Docker) |
| [`014-reliable-messaging`](./014-reliable-messaging) | [014 — Reliable Messaging](https://relay.nuvoralabs.dev/articles/reliable-messaging) | command → outbox → broker → consumer end-to-end | **Yes** (Postgres/Docker) |
| [`015-sagas`](./015-sagas) | [015 — Sagas](https://relay.nuvoralabs.dev/articles/sagas) | `Saga<TState>`, `ISagaConfigurator` (StartedBy/Handle/OnTimeout), `SagaCoordinator`, compensation | No (in-memory) |
| [`016-state-machine-sagas`](./016-state-machine-sagas) | [016 — State Machine Sagas](https://relay.nuvoralabs.dev/articles/state-machine-sagas) | declarative `StateMachineSaga` DSL (Initially/During/When/Then/TransitionTo/Finalize) | No (in-memory) |
| [`017-routing-slips`](./017-routing-slips) | [017 — Routing Slips](https://relay.nuvoralabs.dev/articles/routing-slips) | `RecordCompensation`/`Compensate`, reverse-order (LIFO) rollback of a distributed transaction | No (in-memory) |
| [`018-scheduling`](./018-scheduling) | [018 — Scheduling](https://relay.nuvoralabs.dev/articles/scheduling) | `IScheduler.ScheduleCommandAsync`, due-message dispatch, deferred command delivery | No (in-memory) |
| [`019-recurring-jobs`](./019-recurring-jobs) | [019 — Recurring Jobs](https://relay.nuvoralabs.dev/articles/recurring-jobs) | `IJob`/`IJobHandler`, `JobScheduler`, `CronRecurringOccurrencePlanner` (pure cron math) | No (in-memory) |
| [`020-multi-tenancy`](./020-multi-tenancy) | [020 — Multi-Tenancy](https://relay.nuvoralabs.dev/articles/multi-tenancy) | fail-closed `[TenantScoped]`/`[GlobalOperation]` enforcement, `TenantResolutionMiddleware` | No (in-memory) |
| [`021-observability`](./021-observability) | [021 — Observability](https://relay.nuvoralabs.dev/articles/observability) | `RelayTelemetry` (OpenTelemetry `ActivitySource`+`Meter`); assert metrics/spans via in-proc listeners | No (in-memory) |
| [`022-resiliency`](./022-resiliency) | [022 — Resiliency](https://relay.nuvoralabs.dev/articles/resiliency) | `ConfigurableRetryPolicy`, `CircuitBreaker`, `TokenBucketRateLimiter`, `ConcurrencyLimiter` | No (in-memory) |
| [`023-distributed-coordination`](./023-distributed-coordination) | [023 — Distributed Coordination](https://relay.nuvoralabs.dev/articles/distributed-coordination) | `DefaultPartitioner` (stable hashing), `DistributedLockLeaderElector`, Postgres `pg_advisory_lock` | Partly (lock tests need Docker) |
| [`024-schema-evolution`](./024-schema-evolution) | [024 — Schema Evolution](https://relay.nuvoralabs.dev/articles/schema-evolution) | `IEventUpcaster` (v1→current on read), `[EventType]` stable names, `EventTypeRegistry` | No (in-memory) |
| [`025-reference-architecture`](./025-reference-architecture) | [025 — Reference Architecture](https://relay.nuvoralabs.dev/articles/reference-architecture) | the whole stack: command → event store + outbox (atomic) → projection → query | **Yes** (Postgres/Docker) |

## Prerequisites

- **.NET SDK 10.0** (`net10.0`). Check with `dotnet --version`.
- Some later samples (event store, projections, outbox) run their integration tests against
  **PostgreSQL** and **RabbitMQ** via [Testcontainers](https://dotnet.testcontainers.org/),
  which needs a running Docker daemon. The two samples above need neither.

## Build & test everything

```bash
# from the repository root
dotnet test samples/Relay.Samples.slnx
```

or use the helper script (restores, builds Release, runs tests):

```bash
./samples/build.sh
```

## Run a single sample

```bash
dotnet run --project samples/001-getting-started/Catalog.Api
# then hit the endpoints with curl (the host prints the port on startup):
#   curl http://localhost:5xxx/products
```

Each sample directory has its own `README.md` with concrete `curl` commands and expected output.

## How samples reference the framework

Samples reference the framework's projects directly (not NuGet packages) via the shared
`$(RelaySrc)` property defined in [`Directory.Build.props`](./Directory.Build.props), so they
always build against the source in this repository. Building a sample also builds the framework
projects it depends on.
