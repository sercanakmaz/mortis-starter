# Working on this project

- Read `MORTIS.md` first: it records every choice the project was generated with.
- Every change goes through a command: a struct embedding `shared.WithContext` (and `shared.InTransaction` when it writes), a `Validate` method when it takes input, and one handler registered in `internal/application/module.go`.
- Aggregates change only through their own methods, which return an error and raise events with `Raise`. Ids come from `shared.NewID()`; a random id fails the build.
- A sync handler runs in the command's transaction and its failure rolls the command back; a consumer runs after the commit and its failure is logged. Both react by sending a command with `shared.FromEvent(event)` as its context.
- Repositories write through `Database.On(ctx)`, so they join the command's transaction, and hand the aggregate's events over with `persistence.RaiseEvents`.
- A schema change is a new file in `internal/infrastructure/persistence/migrations/`, never an edit of an applied one.
- No comments except doc comments on declarations; no `init`; no `panic` or printing outside `cmd/`. `gofmt` decides the layout.
- Test names read `Test<Unit>_<Situation>_<Outcome>`; tests use the in-memory doubles in `internal/testsupport`.
