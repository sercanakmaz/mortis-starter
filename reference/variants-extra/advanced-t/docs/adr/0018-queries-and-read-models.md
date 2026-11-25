# 0018 — Reads have their own types; screens that need it get a read model

- Status: accepted
- Date: 2026-11-22
- Context: List screens need paging, filters and sorting, and loading aggregates to map them is slow and couples reads to the write model. Some screens need data from several aggregates at once.

## Decision

A read is a query: `IQuery<TResult>` with the caller's context, a `BaseQueryHandler` with an optional validator, and an `IQueryDispatcher` the hosts use. A query handler reads through a reader port declared next to the repository in Domain, implemented in Infrastructure as a projection to a view record: no tracking, filter, sort and page in the database. List queries have typed fields and a closed sort set; `PageRequest` and `PagedResult<T>` are in Shared. With read models, a table per screen (the example's `order_summaries`) is refreshed by sync event handlers inside the command's transaction, each recomputing the whole row, and a Console command rebuilds it.

## What varies

Which screens need a list query, and which need a read model.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| Reads as commands that load aggregates | Nothing new | List screens load whole aggregates; no paging in the database |
| A generic filter language | A parser and a closed operator set | Typed fields per query are simpler to validate and index |
| Read models updated by event consumers | An outbox, eventual consistency | The example's screen needs to match the write at once |
| **Queries with reader ports; read models refreshed by sync handlers** | Query types, readers, a table and its handlers | n/a |

## Consequences

- Gained: lists page and filter in SQL; reads never touch the unit of work; a read model is consistent with the write and can be rebuilt.
- Lost or owed: a reader and view records per aggregate that is read; data copied into a read model from another aggregate is refreshed only by its own aggregate's events.
- Cost of going back: low.

## How it is enforced

`QueryRulesTests` (no query handler depends on the unit of work, a repository or a domain service), `ProductReaderTests` (the page's SQL), `ReadModelModuleTests`, and `queries.sh`.
