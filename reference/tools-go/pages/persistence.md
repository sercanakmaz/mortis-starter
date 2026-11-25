# PostgreSQL: internal/infrastructure/persistence

`pgx` with hand-written SQL (decision 54). A repository runs its SQL on `Database.On(ctx)`: the command's transaction when there is one, the pool otherwise. After writing an aggregate it hands the aggregate's events to the command with `RaiseEvents`.

@@go internal/infrastructure/persistence/database.go@@

`connection_string.go`: the connection string comes from `ConnectionStrings__Default`, in the URL form, the keyword form, or the semicolon form the C# projects use, so `verify.sh` sets one variable for every language.

@@go internal/infrastructure/persistence/connection_string.go@@

@@go internal/infrastructure/persistence/events.go@@

`unit_of_work.go`: the outermost `InTransaction` begins the transaction, runs the work, runs the sync handlers of every raised event in the same transaction, commits, and only then hands the dispatched events to the publisher. A failure anywhere rolls back and forgets the events. An inner call joins the outer transaction.

@@go internal/infrastructure/persistence/unit_of_work.go@@

## Migrations

Plain SQL files, applied in name order by `go run ./cmd/console migrate`, each in its own transaction and recorded in `schema_migrations`. A schema change is a new file; an applied file is never edited.

@@go internal/infrastructure/persistence/migrate.go@@

With the example, the first migration:

@@go internal/infrastructure/persistence/migrations/0001_initial.sql@@

Without the example, write `0001_initial.sql` containing only `select 1;`: `go:embed` needs at least one file to match.

## Repositories

@@go internal/infrastructure/persistence/products/repository.go@@

The order's repository writes its lines with an upsert, so a new line is inserted and a changed one updated; money goes through `numeric` as text.

@@go internal/infrastructure/persistence/orders/repository.go@@

@@go internal/infrastructure/persistence/customers/repository.go@@

@@go internal/infrastructure/persistence/categories/repository.go@@
