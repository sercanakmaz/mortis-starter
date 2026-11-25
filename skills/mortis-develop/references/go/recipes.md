# The recipes in Go

With `language` = `go` the recipes' rules hold and their C# paths do not. This page maps each C# file, test and command of a recipe to the Go project's one, and says which rules do not apply. The Go pages are under `../mortis-scaffold/references/go/`. Go supports the lean preset only (`options/languages.md`): a recipe that needs an option the Go project cannot have (tenancy, a query side, translated fields, RabbitMQ, the outbox, a read model) is refused in the first line of the answer.

## Commands

```bash
go build ./... && go vet ./...                 # nothing printed is the bar
go test ./...                                  # the whole suite
go test ./internal/domain/suppliers/...        # one package while working
gofmt -l .                                     # prints nothing when formatted
bash <scaffold>/scripts/verify.sh <project>
bash <review>/scripts/review.sh <project>
```

`<scaffold>` and `<review>` are the skill folders, `<project>` the project folder; use absolute paths, because the work happens in the project. There is no migration tool: a migration is the next numbered file, `internal/infrastructure/persistence/migrations/000N_<change>.sql` (`0002_suppliers.sql`), applied by `console migrate`. Names follow `0001_initial.sql`: `fk_<table>_<referenced>_<column>`, `ak_<table>_<column>` for a unique key, `ck_<table>_<rule>`, `ix_<table>_<columns>` for an index (unique or not). Never edit a migration that ran anywhere outside a developer's machine.

## Where a C# file of a recipe goes

| C# (recipe) | Go | Page |
|---|---|---|
| `Domain/Aggregates/{Plural}/{Aggregate}.cs`, its constants, events, repository interface, value objects | package `internal/domain/{plural}/`: `{aggregate}.go`, `constants.go`, `events.go` (every event of the aggregate, bare names, `EventName()` returning `{Name}.Domain.Aggregates.{Plural}.Events:<Event>` with the project's literal name), `repository.go` (`type Repository interface`), one file per value object | `domain.md` |
| a child entity (`{Child}.cs`) | `{child}.go` in the same package; the aggregate keeps `[]*{Child}` and changes it only through its own methods | `domain.md` "orders" |
| `Application/{Plural}/{Verb}{Aggregate}Command.cs` (command, validator, handler) | `internal/application/{plural}/{verb}_{aggregate}.go`: the command struct with `Validate()`, the handler struct with `Handle`; `dto.go` holds `{Aggregate}DTO` and `Map{Aggregate}` | `application.md` |
| the `ApplicationModule` line | `shared.Register(dispatcher, {plural}.{Verb}{Aggregate}Handler{...})` in `internal/application/module.go`, and the repository in its `Repositories` struct | `application.md` |
| a sync event handler | `{action}_when_{event}.go` in the reacting package; `events.AddSync(...)` in `module.go` | `events-inprocess.md` |
| an event consumer (`AddEventConsumer<T>(... Retry(n))`) | `{action}_when_{event}.go`, type `{Action}When{Event}Consumer`; `events.AddConsumer(...)` in `module.go`. No host line and no retry: in-process consumers run once, after the commit | `events-inprocess.md` |
| `Infrastructure/Persistence/{Plural}/{Aggregate}Repository.cs`, its configuration, the `PersistenceModule` line | `internal/infrastructure/persistence/{plural}/repository.go` with hand-written SQL (children written with `insert ... on conflict (id) do update`, as the example's `writeLines`); the line in `internal/bootstrap/bootstrap.go` | `persistence.md` |
| a migration (`dotnet ef migrations add`) | the next `migrations/000N_<change>.sql` | `persistence.md` "Migrations" |
| the controller | routes in `internal/api/routes.go` (`mux.HandleFunc("POST /{plural}", ...)`), route path by `naming.routes` | `hosts.md` |
| `Fake{Aggregate}`, `{Port}Builder`, `{Aggregate}RepositoryBuilder` | an in-memory store in `internal/testsupport/repositories.go` (`New{Plural}(items ...)`), which the change extends; aggregates for tests are made with the domain's own constructor | `testing.md` |
| domain tests (`{Aggregate}Tests.cs`) | `internal/domain/{plural}/{aggregate}_test.go`, package `{plural}_test` | `testing.md` |
| handler tests | `internal/application/{plural}/{verb}_{aggregate}_test.go`, package `{plural}_test`, with the testsupport stores and `testsupport.UnitOfWork`; a consumer is tested by calling its `Handle` with the event | `testing.md` |
| configuration and repository tests | none: database behaviour is proved by `verify.sh` for the example and by a Console run for a new aggregate (say so in the answer) | `testing.md` |

## Rules that change

- **Names:** `naming.md` of the Go pages, not `canonical-shapes.md`: files in snake case, tests `Test<Unit>_<Situation>_<Outcome>`, a command used only for a reaction keeps the C# name pattern `{Verb}{Aggregate}When{Condition}Command`.
- **Children:** Go has no `internal` constructor. The child's fields are exported for the repository's scan, as in the example's `orders.Line`; the rule becomes "only the aggregate's methods add, change or remove a child", and `review.sh` does not check it for Go.
- **Events:** there is no `IQueueEvent`; any event may have a consumer. With `events.outbox` = `none` (always, in Go) a consumer's work is lost if the process stops between the commit and the consumer: say so when a reaction must not be lost, and make it a sync handler instead.
- **Which handler:** a reaction that must succeed or fail with the command is a sync handler; one that may happen afterwards is a consumer. When the request does not say, choose the sync handler and say why in the answer.
- **AGENTS numbers:** the Go project's `AGENTS.md` has no numbered rules; a recipe's `AGENTS n` means the rule with that meaning in its list.
- **Order of recipes for an aggregate with children and a create command:** `add-aggregate.md` rows 1 to 9, then `add-child-entity.md`, then rows 10 to 15 (the command takes the children).

## Not available in Go

`scripts/new_aggregate.sh` prints C# paths; with `language` = `go` it prints this page's table instead. `change-a-key-type.md`, `make-aggregate-tenant-owned.md`, `add-query.md`, `add-read-model.md`, `add-translated-field.md` and `turn-on-an-option.md` for anything but the lean values are refused: the Go pages do not have them.
