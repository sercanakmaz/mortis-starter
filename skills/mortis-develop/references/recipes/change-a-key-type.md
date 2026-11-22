# Change an aggregate's key type

From `Guid` to `int` or `long`, or back. Every reference to the aggregate changes with it.

| # | File | Page | Rule | Test |
|---|---|---|---|---|
| 1 | the aggregate: `AggregateRoot<TKey>` with the new type | `worked-slice.md` (ADR 0008) | AGENTS 4 | the aggregate's tests |
| 2 | [database=mongo] stop: MongoDB keeps `Guid` keys (R12) | `infrastructure-mongo.md` | R12 | none |
| 3 | to `int` or `long`: the database assigns the key, `Created` moves from the constructor to `MarkCreated()` called by the repository after the insert | `worked-slice.md` | AGENTS 4 | the repository test: the event carries the assigned id |
| 4 | to `Guid`: the domain assigns `Guid.CreateVersion7()` in the constructor, `ValueGeneratedNever()` | `worked-slice.md` | AGENTS 4 | the aggregate's test |
| 5 | `I{Aggregate}Repository`, the repository, the configuration, every command, `Dto`, route constraint (`{id:int}`) and foreign key that names the key | `worked-slice.md`, `hosts.md` | AGENTS 12 | every handler test of the aggregate |
| 6 | every event carrying the id as a string: nothing; every consumer parsing it: the new parse | `event-consumers.md` | AGENTS 3 | the consumer tests |
| 7 | [events.transport=rabbitmq] a published event's id format changes for every other consumer: record it in the wire-format spec | `rabbitmq.md` | none | `transport-rabbitmq.sh` |
| 8 | [database=postgres] a migration; with data, a new table and a copy written by hand | `infrastructure-postgres.md` "Migrations" | none | `verify.sh` |
