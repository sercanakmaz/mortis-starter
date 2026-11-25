# Boilerplate

A Go backend generated from `MORTIS.md` (preset lean): commands through a dispatcher, aggregates that raise events, PostgreSQL through pgx, consumers that run in-process after the command commits.

## Run it

```bash
docker compose up -d postgres
export ConnectionStrings__Default="Host=localhost;Port=5432;Database=boilerplate;Username=postgres;Password=postgres"
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
