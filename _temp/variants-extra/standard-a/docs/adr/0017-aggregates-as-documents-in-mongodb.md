# 0017 — Aggregates are documents in MongoDB

- Status: accepted
- Date: 2026-11-20
- Context: The aggregates fit one document each, and the team does not want a relational mapping or migrations. The outbox and the units of work still need atomic writes across documents.

## Decision

Each aggregate is one document, with its child entities and value objects embedded; references between aggregates are ids. Class maps in Infrastructure keep the Domain free of driver attributes. `MongoRepository` implements the same `IRepository`: a `version` number on each document, checked on every replace, gives optimistic concurrency. The unit of work is a client session with a multi-document transaction, so the server runs as a replica set (a single node in development). The outbox is a collection written in the same session. Unique indexes replace unique constraints; they are ensured at start in Development and by `dotnet run -- ensure-indexes` in deployment. Integer keys come from a counter collection only where the Domain fixes them; `keys.default` stays `guid`.

## What varies

Which fields are indexed, and how a document's shape changes over time.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| PostgreSQL with EF Core | Migrations and a relational mapping | The team chose documents |
| MongoDB without transactions | Nothing to configure | The outbox and the command could disagree after a crash |
| **MongoDB with a replica set and session transactions** | A replica set, a version field, class maps | n/a |

## Consequences

- Gained: no migrations; an aggregate is read and written in one operation; the Domain and Application code is the same as with PostgreSQL.
- Lost or owed: no foreign keys, so a reference to a missing aggregate is not refused by the database; a standalone server cannot run the project; a document shape change needs an upgrade on read.
- Cost of going back: high, because the data must be moved.

## How it is enforced

`MongoClassMapsTests` (standard UUIDs, embedded lines), `MongoRepositoryRulesTests` (every query through the repository base), and `database-mongo.sh` (the example's flows read back from the documents, rollback, concurrent updates, duplicates).
