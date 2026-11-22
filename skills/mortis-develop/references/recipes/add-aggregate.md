# Add an aggregate

A new aggregate root with its repository, configuration, first command, controller and tests. Paths are C#'s; `{Aggregate}` is singular, `{Plural}` its folder.

| # | File | Page | Rule | Test |
|---|---|---|---|---|
| 1 | `test/{Name}.Tests/TestSupport/Domain/{Plural}/Fake{Aggregate}.cs`, `{Aggregate}RepositoryBuilder.cs` | `worked-slice-tests.md` | AGENTS 14 | none (support) |
| 2 | `test/{Name}.Tests/Domain/{Plural}/{Aggregate}Tests.cs` | `worked-slice-tests.md` | AGENTS 1, 3 | the constructor raises `Created`; every rule of the aggregate throws its coded exception |
| 3 | `src/{Name}.Domain/Aggregates/{Plural}/{Aggregate}.cs`, `{Aggregate}Constants.cs`, `Events/Created.cs` | `worked-slice.md`, `domain.md` | AGENTS 1, 3, 4 | step 2 green |
| 4 | `src/{Name}.Domain/Aggregates/{Plural}/I{Aggregate}Repository.cs` | `worked-slice.md` | AGENTS 6, 7 | none (port) |
| 5 | [keys.default=int\|long] `MarkCreated()` raises `Created` after the insert, never the constructor | `worked-slice.md` (ADR 0008) | AGENTS 4 | the repository test of step 7 |
| 6 | [tenancy.mode=interface] the aggregate implements `IMultiTenant` | `make-aggregate-tenant-owned.md` (this skill) | R13 | that recipe's tests |
| 7 | `test/{Name}.Tests/Infrastructure/Persistence/{Plural}/{Aggregate}ConfigurationTests.cs`, `{Aggregate}RepositoryTests.cs` | `worked-slice-tests.md` | AGENTS 12 | keys, lengths, foreign keys; events handed to the event context before saving |
| 8 | [database=postgres] `src/{Name}.Infrastructure/Persistence/{Plural}/{Aggregate}Configuration.cs`, `{Aggregate}Repository.cs`, one line in `PersistenceModule.cs` | `infrastructure-postgres.md` | AGENTS 12, 13 | step 7 green |
| 9 | [database=mongo] `src/{Name}.Infrastructure/Persistence/Mongo/{Plural}/{Aggregate}Repository.cs`, its class map and indexes | `infrastructure-mongo.md` | AGENTS 13 | the class map and repository rules tests |
| 10 | the first command: `add-command.md` (this skill) | `application.md` | AGENTS 5, 10, 20 | that recipe's tests |
| 11 | `src/{Name}.Api/Controllers/{Plural}Controller.cs` | `hosts.md` | AGENTS 10 | the command's handler test; a request in `verify.sh` |
| 12 | [queries=split\|read-models] the reader port, view and reader: `add-query.md` (this skill) | `queries.md` | AGENTS 11 | that recipe's tests |
| 13 | [addons=soft-delete] `ISoftDeletable`, delete and restore commands, the filtered unique index | `soft-delete.md` | AGENTS 1 | `soft-delete.md` tests |
| 14 | [addons=contexts] every path above under `Contexts/{Context}/` | `contexts.md` | `ContextRulesTests` | `ContextRulesTests` |
| 15 | [database=postgres] a migration | `infrastructure-postgres.md` "Migrations" | none | `verify.sh` |

An aggregate never gets an `{Aggregate}Service`. A rule that needs another aggregate's data is a domain service with a read port (decision 31).
