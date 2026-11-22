# Add a child entity

An entity that exists only inside its aggregate root (an order's line). It gets no repository and no command of its own.

| # | File | Page | Rule | Test |
|---|---|---|---|---|
| 1 | `test/{Name}.Tests/Domain/{Plural}/{Aggregate}Tests.cs`: tests of the root's operations that add, change or remove the child | `worked-slice-tests.md` | AGENTS 1, 2 | the root's method changes the child and raises its event; a rule on the children throws |
| 2 | `src/{Name}.Domain/Aggregates/{Plural}/{Aggregate}{Part}.cs`: `internal` constructor and mutating methods | `worked-slice.md` (`OrderLine`) | AGENTS 2 | compiler |
| 3 | `src/{Name}.Domain/Aggregates/{Plural}/{Aggregate}.cs`: a private `List<{Aggregate}{Part}>` exposed as `IReadOnlyCollection`, and the operations | `worked-slice.md` | AGENTS 1, 2 | step 1 green |
| 4 | an event per operation: `add-event.md` (this skill) | `worked-slice.md` | AGENTS 3 | the root's tests |
| 5 | `test/.../Infrastructure/Persistence/{Plural}/{Aggregate}ConfigurationTests.cs`; `{Aggregate}Configuration.cs`: `OwnsMany` or `HasMany` with cascade, the backing field | `infrastructure-postgres.md` | AGENTS 12 | the configuration test |
| 6 | [tenancy.mode=interface] the child's table carries `tenant_id` when it has its own table | `tenancy.md` | R13 | the configuration test |
| 7 | the root's `Dto` maps the children | `application.md` | none | the handler tests |
| 8 | [database=postgres] a migration | `infrastructure-postgres.md` "Migrations" | none | `verify.sh` |
