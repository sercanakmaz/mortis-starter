# The module and the root files

Go 1.24 or later (the reference solution's `go.mod` asks for 1.25.0 because `pgx` 5.13 does; `GOTOOLCHAIN=auto` fetches it). Three dependencies:

```bash
go mod init {module}
go get github.com/google/uuid@v1.6.0 github.com/jackc/pgx/v5@v5.13.0 github.com/shopspring/decimal@v1.6.0
go mod tidy
```

Until the code imports them, `go get` records them as indirect and leaves `go.sum` incomplete; `go mod tidy` at step 7 settles both. After step 7, `go.mod` reads:

```
module {module}

go 1.25.0

require (
	github.com/google/uuid v1.6.0
	github.com/jackc/pgx/v5 v5.13.0
	github.com/shopspring/decimal v1.6.0
)

require (
	github.com/jackc/pgpassfile v1.0.0 // indirect
	github.com/jackc/pgservicefile v0.0.0-20240606120523-5a60cdf6a761 // indirect
	github.com/jackc/puddle/v2 v2.2.3 // indirect
	golang.org/x/sync v0.22.0 // indirect
	golang.org/x/text v0.41.0 // indirect
)
```

The root files, written as shown.

`.gitignore`:

```
.mortis/
*.test
coverage.out
```

`docker-compose.yml`:

```yaml
services:
  postgres:
    image: postgres:16
    environment:
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: postgres
      POSTGRES_DB: {module}
    ports:
      - "5432:5432"
```

`README.md`:

```
# {Name}

A Go backend generated from `MORTIS.md` (preset lean): commands through a dispatcher, aggregates that raise events, PostgreSQL through pgx, consumers that run in-process after the command commits.

## Run it

```bash
docker compose up -d postgres
export ConnectionStrings__Default="Host=localhost;Port=5432;Database={module};Username=postgres;Password=postgres"
go run ./cmd/console migrate
go run ./cmd/api                      # listens on localhost:5000, or $MORTIS_API_ADDRESS
go run ./cmd/console --user developer CreateProduct '{"sku":"SKU-1","name":"Bolt","stock":10}'
```

`go test ./...` runs the unit tests; they need no database.

## Where things are

```
internal/shared                      the kernel: dispatcher, pipeline, events, errors, validation
internal/domain/<aggregate>          aggregates, their events and repository interfaces
internal/application/<aggregate>     commands, handlers, DTOs, sync handlers and consumers
internal/infrastructure              PostgreSQL, the unit of work, migrations, logging, the event publisher
internal/api, cmd/api                the HTTP host
cmd/console                          runs any registered command by name, and applies the migrations
internal/testsupport                 in-memory repositories and unit of work for the tests
```

`AGENTS.md` has the rules for changing it.
```

`AGENTS.md`:

```
# Working on this project

- Read `MORTIS.md` first: it records every choice the project was generated with.
- Every change goes through a command: a struct embedding `shared.WithContext` (and `shared.InTransaction` when it writes), a `Validate` method when it takes input, and one handler registered in `internal/application/module.go`.
- Aggregates change only through their own methods, which return an error and raise events with `Raise`. Ids come from `shared.NewID()`; a random id fails the build.
- A sync handler runs in the command's transaction and its failure rolls the command back; a consumer runs after the commit and its failure is logged. Both react by sending a command with `shared.FromEvent(event)` as its context.
- Repositories write through `Database.On(ctx)`, so they join the command's transaction, and hand the aggregate's events over with `persistence.RaiseEvents`.
- A schema change is a new file in `internal/infrastructure/persistence/migrations/`, never an edit of an applied one.
- No comments except doc comments on declarations; no `init`; no `panic` or printing outside `cmd/`. `gofmt` decides the layout.
- Test names read `Test<Unit>_<Situation>_<Outcome>`; tests use the in-memory doubles in `internal/testsupport`.
```

Copy `MORTIS.md` into the root unchanged. `go.sum` is written by `go mod tidy`, never by hand.
