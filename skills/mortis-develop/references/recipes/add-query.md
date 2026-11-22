# Add a query

A read with its own types, when `queries` is `split` or `read-models`. With `queries` = `commands`, a read is `add-command.md` with a `Get{Aggregate}Command`.

| # | File | Page | Rule | Test |
|---|---|---|---|---|
| 1 | [queries=split\|read-models] `src/{Name}.Domain/Aggregates/{Plural}/I{Aggregate}Reader.cs`, `{Aggregate}View.cs` | `queries.md` | AGENTS 11 | none (port) |
| 2 | [queries=split\|read-models] `test/.../Application/{Plural}/{Verb}{Aggregate}QueryHandlerTests.cs` | `queries.md` | AGENTS 14 | validation before any read; the view mapped |
| 3 | [queries=split\|read-models] `src/{Name}.Application/{Plural}/{Verb}{Aggregate}Query.cs`, its validator and handler; `AddQueryHandler` in `ApplicationModule` | `queries.md` | AGENTS 13 | step 2 green |
| 4 | [queries=split\|read-models] `src/{Name}.Infrastructure/Persistence/{Plural}/{Aggregate}Reader.cs`: no tracking, a projection, sort ending on a unique column | `queries.md` | none | `{Aggregate}ReaderTests` |
| 5 | [queries=split\|read-models] the controller action through `IQueryDispatcher` | `queries.md` | AGENTS 10 | `queries.sh` |
| 6 | [queries=read-models] a screen that needs its own table: `add-read-model.md` (this skill) | `read-models.md` | R10 | that recipe |
| 7 | [addons=caching] the handler reads through `ICacheService` | `caching.md` | none | the handler test with a pass-through cache |
