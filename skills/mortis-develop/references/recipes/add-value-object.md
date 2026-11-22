# Add a value object

An immutable value that validates itself (money, an address, a quantity with a unit).

| # | File | Page | Rule | Test |
|---|---|---|---|---|
| 1 | `test/{Name}.Tests/Domain/{Plural}/{ValueObject}Tests.cs` | `worked-slice-tests.md` (`MoneyTests`) | AGENTS 2 | an invalid value throws `InvalidParameterException`; equality by value |
| 2 | `src/{Name}.Domain/Aggregates/{Plural}/{ValueObject}.cs`: a `record` validating in its constructor | `worked-slice.md` (`Money`) | AGENTS 2 | step 1 green |
| 3 | the aggregate takes and exposes it | `worked-slice.md` | AGENTS 1 | the aggregate's tests |
| 4 | `{Aggregate}Configuration.cs`: `ComplexProperty` or `OwnsOne`, column names | `infrastructure-postgres.md` | AGENTS 12 | the configuration test |
| 5 | the `Dto` flattens it; the command takes its parts | `application.md` | AGENTS 11 | the handler tests |
| 6 | [database=postgres] a migration | `infrastructure-postgres.md` "Migrations" | none | `verify.sh` |

A value object two aggregates use moves to `{Name}.Shared/Domain`.
