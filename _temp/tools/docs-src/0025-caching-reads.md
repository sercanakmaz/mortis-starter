# 0025 — Read answers are cached and invalidated by events

- Status: accepted
- Date: <the date the project was generated>
- Context: Some reads are slow and repeated. A cache that is not invalidated by the writes serves stale data, and a cache feeding a write corrupts it.

## Decision

Read handlers cache their answers through `ICacheService` (HybridCache: memory in each process, Redis behind it when configured), keyed by plural, tenant, id and language and tagged by aggregate instance. Every event that changes a cached answer has a sync handler that removes the tag. Commands always read the database.

## What varies

What is cached and for how long (`Caching:Seconds`).

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| No cache | Slower reads | Chosen against for the reads that need it |
| Time-based expiry only | Nothing to invalidate | Answers are stale for the whole lifetime |
| Cache the aggregate | One cache for reads and writes | A stale aggregate would feed a write |
| **Cache answers, invalidate by events** | A handler per changing event | n/a |

## Consequences

- Gained: repeated reads skip the database; a caller who just wrote reads fresh data.
- Lost or owed: a short window (the lifetime) where a concurrent read can put an old answer back; without Redis each process invalidates only its own memory.
- Cost of going back: low.

## How it is enforced

`HybridCacheServiceTests`, the invalidation handler tests, and `caching.sh` (a read after a change is fresh).
