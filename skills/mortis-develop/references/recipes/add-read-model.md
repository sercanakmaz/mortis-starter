# Add a read model

A table shaped for one screen, refreshed from events. Needs `queries` = `read-models` and an outbox (R10).

| # | File | Page | Rule | Test |
|---|---|---|---|---|
| 1 | [queries=read-models] `src/{Name}.Infrastructure/ReadModels/{Screen}s/{Screen}.cs`, `{Screen}Configuration.cs` | `read-models.md` | R10 | the configuration test |
| 2 | [queries=read-models] `{Screen}Projection.cs`: recompute the whole row from the aggregate tables | `read-models.md` | none | the projection test against PostgreSQL |
| 3 | [queries=read-models] `{Screen}Refresh<TEvent>` per event of every aggregate the row copies | `read-models.md` | none | each refresh handler's test |
| 4 | [queries=read-models] `{Screen}Reader` and the query that reads it: `add-query.md` (this skill) | `read-models.md`, `queries.md` | none | the reader test |
| 5 | [queries=read-models] the console command `rebuild-{screen}` | `read-models.md` | none | `queries.sh` |
| 6 | [tenancy.mode=interface] the row carries `tenant_id` and the tenant filter | `tenancy.md` | R13 | a cross-tenant read returns nothing |
| 7 | [database=postgres] a migration | `infrastructure-postgres.md` "Migrations" | none | `verify.sh` |
