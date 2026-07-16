# Sample 009 — Caching Queries

Companion to **[Article 009 — Caching Queries](../../docs/articles/009-caching-queries.md)**.

A database-free console app showing Relay's query caching:

- `[Cacheable(DurationSeconds = 60)]` on `GetCatalogStatsQuery` makes the built-in
  `QueryCachingBehavior` cache the result (keyed by query type + parameters, with stampede protection).
- A `QueryExecutionCounter` in the handler proves caching **deterministically**: a cache hit skips the
  handler, so the counter stays put.
- `RecordSaleCommand` mutates the data and calls
  `IQueryCacheInvalidator.InvalidateQueryAsync<GetCatalogStatsQuery>()`, so the next read recomputes.

## Layout

```
Reporting/
  Catalog/Queries.cs    # [Cacheable] query + handler (increments the counter on a miss)
  Catalog/Commands.cs   # RecordSaleCommand: mutate + invalidate
  Catalog/CatalogStore.cs # the store + the QueryExecutionCounter
  ReportingServiceCollectionExtensions.cs # AddRelay + AddRelayInMemoryCache
  Program.cs            # runs the cache-hit + invalidation scenario
Reporting.Tests/
  CachingTests.cs       # asserts the handler runs once for two queries, twice after invalidation
```

## Run it

```bash
dotnet run --project samples/009-caching-queries/Reporting
```

```
After two identical queries, the handler ran 1 time(s).
After a sale + invalidation, the handler ran 2 time(s); revenue = 49.99.
```

## Test it

```bash
dotnet test samples/009-caching-queries/Reporting.Tests
```

> Requires the **.NET 10 SDK**. No Docker/database needed. Swap `AddRelayInMemoryCache()` for
> `AddRelayRedisCache(...)` to share the cache across instances — the behavior and invalidation are
> identical.
