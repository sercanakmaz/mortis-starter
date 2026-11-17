# Naming, C#

Every name in a generated C# project comes from this page. Nothing is named by taste. `verify.sh` checks types, files and folders against it.

## C#'s own rules

These are the language's rules, not choices.

| What | Rule | Example |
|---|---|---|
| Types, methods, properties, constants | `PascalCase` | `OrderLine`, `AddLine`, `MaxAttempts` |
| Parameters, locals | `camelCase`, named after what they hold | `orderLine`, `commandResult` |
| Private fields | `_camelCase` | `_domainEvents` |
| Interfaces | `I` + `PascalCase` | `IOrderRepository` |
| Generic parameters | `T` + `PascalCase` | `TCommand`, `TContent`, `TKey` |
| Files | one public type per file, named after it; a command, its validator and its handler share the command's file | `CreateOrderCommand.cs` |
| Namespaces | follow the folder path from the project root, file-scoped | `{Name}.Domain.Aggregates.Orders` |
| Async methods | end in `Async`, except the overridden `Handle`, `OnHandle`, `Send`, `Publish`, `Invoke` | `FindByIdTrackingAsync` |
| Test methods | `Method_Condition_Should_Result`, or `Method_Should_Result` | `Complete_WhenNotOpen_Should_ThrowNotOpen` |

The event parameter is `@event`. A local is never named `x`, `item`, `result` or `data` when it holds something with a name.

## Role names

Layer 1 names roles (see `mortis-architect/references/architecture/glossary.md`). This table turns each role into a C# name and path. `{Name}` is the project name, `{Aggregate}` the aggregate root (singular), `{Plural}` its folder (plural), `{Action}` a verb, `{Event}` a past-tense fact.

| Role | Type name | File |
|---|---|---|
| aggregate root | `{Aggregate}` | `src/{Name}.Domain/Aggregates/{Plural}/{Aggregate}.cs` |
| child entity | a noun, e.g. `OrderLine` | same folder |
| value object | a noun, e.g. `Money` | same folder |
| status of an aggregate | `{Aggregate}Status` (enum) | same folder |
| constants of an aggregate (rule codes, lengths) | `{Aggregate}Constants` | same folder |
| input shape shared by commands | `{Noun}Model` | same folder |
| domain event | `{Event}` (bare, past tense) | `src/{Name}.Domain/Aggregates/{Plural}/Events/{Event}.cs` |
| repository interface | `I{Aggregate}Repository` | `src/{Name}.Domain/Aggregates/{Plural}/I{Aggregate}Repository.cs` |
| domain service | the concept it serves, e.g. `PricingService`, `TransferPolicy`; never `{Aggregate}Service` | `src/{Name}.Domain/Services/{Type}.cs` |
| anti-corruption port | the question, e.g. `ITaxRates` | `src/{Name}.Domain/Ports/{Type}.cs` |
| command | `{Action}{Aggregate}Command`, or `{Action}When{Condition}Command` for a reaction | `src/{Name}.Application/{Plural}/{Command}.cs` |
| validator | `{Command}Validator` | in the command's file |
| command handler | `{Command}Handler` | in the command's file |
| view (DTO) | `{Aggregate}Dto`, `{Child}Dto` | `src/{Name}.Application/{Plural}/{Aggregate}Dto.cs` |
| event handler | `{Action}When{Aggregate}{Event}EventHandler` | `src/{Name}.Application/{ReactingPlural}/{Handler}.cs` |
| repository | `{Aggregate}Repository` | `src/{Name}.Infrastructure/Persistence/{Plural}/{Aggregate}Repository.cs` |
| storage mapping | `{Aggregate}Configuration`, `{Child}Configuration` | `src/{Name}.Infrastructure/Persistence/{Plural}/{Aggregate}Configuration.cs` |
| anti-corruption adapter | the outside system plus the port, e.g. `HttpTaxRates` | `src/{Name}.Infrastructure/Integrations/{Type}.cs` |
| controller | `{Plural}Controller` | `src/{Name}.Api/Controllers/{Plural}Controller.cs` |
| console scenario | `{Flow}Scenario`, run name in kebab case (`order-flow`) | `src/{Name}.Console/Scenarios/{Flow}Scenario.cs` |
| registration of a layer | `{Layer}Module` with `Add{Layer}Module` | the layer's project root |
| test class | `{Type}Tests` | mirrors the type's path under `test/{Name}.Tests/` |
| fake aggregate for tests | `Fake{Aggregate}` | `test/{Name}.Tests/TestSupport/Domain/{Plural}/Fake{Aggregate}.cs` |
| builder of a mock | `{Interface without I}Builder` | `test/{Name}.Tests/TestSupport/<mirrored path>/{Type}Builder.cs` |

A command's name says what the user asked for. A reaction's command says what happens and when (`CancelOrderWhenAllLinesCancelledCommand`). A query is a command whose name starts with `Get`.

## Project choices from `MORTIS.md`

| Key | Value | What it changes in C# |
|---|---|---|
| `naming.database` | `snake_case` | `UseSnakeCaseNamingConvention()` on the context; tables are named explicitly in snake case: `builder.ToTable("order_lines")` |
| | `as-code` | no naming convention; `builder.ToTable("OrderLines")` |
| `naming.json` | `camelCase` | the ASP.NET default; nothing to add |
| | `snake_case` | `options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower` in `AddControllers().AddJsonOptions(...)` |
| `naming.routes` | `kebab-case` | `[Route("order-lines")]` for multi-word resources |
| | `lowercase` | `[Route("orderlines")]` |
| `naming.events` | `bare` | events named `{Event}` in the aggregate's own `Events` namespace; the wire name is `{Name}.Domain.Aggregates.{Plural}.Events:{Event}` |
| | `prefixed` | events named `{Aggregate}{Event}` (`OrderCompleted`); the wire name changes with it |

## Words that are never used

`Manager`, `Helper`, `Util`, `Utils`, `Processor`, `Base` for a class that is not abstract, `Impl`, `Data` as a suffix, `Info` as a suffix, `{Aggregate}Service`.
