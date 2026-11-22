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
| sync event handler | `{Action}When{Aggregate}{Event}EventHandler` | `src/{Name}.Application/{ReactingPlural}/{Handler}.cs` |
| event consumer | `{Action}When{Aggregate}{Event}EventConsumer`; its queue is the name in kebab case without `EventConsumer` | `src/{Name}.Application/{ReactingPlural}/{Consumer}.cs` |
| registering event consumers | `EventConsumerEndpoint`, `EventConsumerModule` with `AddEventConsumer<TConsumer>`, and `EventConsumerRegistry` without a broker (`event-consumers.md`) | `src/{Name}.Infrastructure/Messaging/` |
| repository | `{Aggregate}Repository` | `src/{Name}.Infrastructure/Persistence/{Plural}/{Aggregate}Repository.cs` |
| storage mapping | `{Aggregate}Configuration`, `{Child}Configuration` | `src/{Name}.Infrastructure/Persistence/{Plural}/{Aggregate}Configuration.cs` |
| anti-corruption adapter | the outside system plus the port, e.g. `HttpTaxRates` | `src/{Name}.Infrastructure/Integrations/{Type}.cs` |
| controller | `{Plural}Controller` | `src/{Name}.Api/Controllers/{Plural}Controller.cs` |
| registration of a layer | `{Layer}Module` with `Add{Layer}Module` | the layer's project root |
| publishing committed events | `EventPublisher` (`none`) or `OutboxRepository` + `OutboxWorker` (an outbox), and with RabbitMQ the types on `rabbitmq.md` | `src/{Name}.Infrastructure/Messaging/`, its `Outbox/` or its `RabbitMq/` folder |
| outbox types | the fixed names on `outbox.md` | `src/{Name}.Infrastructure/Messaging/Outbox/` |
| authentication types | `AuthSettings`; `JwtAuthenticationModule` with `AddJwtAuthentication`; `ConsoleTokens` (`authentication.md`) | `src/{Name}.Infrastructure/Authentication/`, `src/{Name}.Api/Authentication/`, `src/{Name}.Console/` |
| language types | `Languages` (generated constants), `MessageLocalizer`, `messages.<language>.json`, `RequestLanguage`; with `data`, `LocalizedText` and `LocalizedTextConversion` (`languages.md`) | `src/{Name}.Shared/Application/`, `src/{Name}.Infrastructure/Localization/`, `src/{Name}.Api/Middlewares/`, `src/{Name}.Shared/Domain/` |
| queries (`queries` = `split` or `read-models`) | `{Verb}{Aggregate}Query` / `List{Plural}Query`, its handler `...QueryHandler`, its validator `...QueryValidator`; the port `I{Aggregate}Reader` and `{Aggregate}View` in Domain, `{Aggregate}Reader` in Infrastructure; `{Aggregate}Sort` (`queries.md`) | `src/{Name}.Application/{Plural}/`, `src/{Name}.Domain/Aggregates/{Plural}/`, `src/{Name}.Infrastructure/Persistence/{Plural}/` |
| read models (`queries` = `read-models`) | `{Screen}` (the row), `{Screen}Configuration`, `{Screen}Projection`, `{Screen}Refresh<TEvent>`, `{Screen}Reader`; table snake case plural (`order_summaries`); rebuild command `rebuild-{screen}` (`read-models.md`) | `src/{Name}.Infrastructure/ReadModels/{Screen}s/` |
| MongoDB types (`database` = `mongo`) | `MongoModule`, `MongoClassMaps`, `MongoUnitOfWork`, `MongoRepository<TAggregate, TKey>`, `MongoExceptionHandler`, `ConcurrencyConflictException`, `MongoIndexes`, `MongoIndexesService`; collections snake case plural (`products`, `outbox_messages`), elements camelCase (`infrastructure-mongo.md`) | `src/{Name}.Infrastructure/Persistence/Mongo/`, one folder per aggregate for its repository |
| tenancy types | `IMultiTenant`, `TenantRequiredException`, `TenantScope`, `TenantCommandHandlerMiddleware` (`tenancy.md`) | `src/{Name}.Shared/Domain/`, `src/{Name}.Shared/Exceptions/`, `src/{Name}.Infrastructure/Tenancy/` |
| worker host | `{Name}.Worker` | `src/{Name}.Worker/` |
| receive endpoint | `{database}-worker`; its error queue `{database}-worker_error` | `AddReceiveEndpoint` in the Worker's `Program.cs` |
| message exchange | `<Namespace>:<TypeName>` of the event | declared by `RabbitMqPublisher` and `ReceiveEndpointService` |
| console command runner | `ConsoleCommandRunner`, `ConsoleConstants` | `src/{Name}.Console/` |
| log formatting | `JsonLineFormatter`, `LogFields`, `LoggingModule` | `src/{Name}.Infrastructure/Logging/` |
| test class | `{Type}Tests` | mirrors the type's path under `test/{Name}.Tests/` |
| fake aggregate for tests | `Fake{Aggregate}` | `test/{Name}.Tests/TestSupport/Domain/{Plural}/Fake{Aggregate}.cs` |
| builder of a mock | `{Interface without I}Builder` | `test/{Name}.Tests/TestSupport/<mirrored path>/{Type}Builder.cs` |

A command's name says what the user asked for. A reaction's command says what happens and when (`CancelOrderWhenAllLinesCancelledCommand`). A query is a command whose name starts with `Get`.

## Project choices from `MORTIS.md`

| Key | Value | What it changes in C# |
|---|---|---|
| `naming.database` | `snake_case` | `UseSnakeCaseNamingConvention()` on the context; tables are named explicitly in snake case: `builder.ToTable("order_lines")` |
| | `as-code` | no naming convention; `builder.ToTable("OrderLines")`; the outbox's raw SQL (`outbox.md`) is written for `snake_case` and must quote the as-code names |
| `naming.json` | `camelCase` | the ASP.NET default; nothing to add |
| | `snake_case` | `options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower` in `AddControllers().AddJsonOptions(...)` |
| `naming.routes` | `kebab-case` | `[Route("order-lines")]` for multi-word resources |
| | `lowercase` | `[Route("orderlines")]` |
| `naming.events` | `bare` | events named `{Event}` in the aggregate's own `Events` namespace; the wire name is `{Name}.Domain.Aggregates.{Plural}.Events:{Event}` |
| | `prefixed` | events named `{Aggregate}{Event}` (`OrderCompleted`); the wire name changes with it |

## Words that are never used

`Manager`, `Helper`, `Util`, `Utils`, `Processor`, `Base` for a class that is not abstract, `Impl`, `Data` as a suffix, `Info` as a suffix, `{Aggregate}Service`.
