# The kernel: internal/shared

One package, `shared`, with no dependency on any other package of the project. `verify.sh` compares every exported declaration of these files with `kernel-shape.txt`; write them exactly.

## Ids, users, context

`ids.go`: the only place a new id is made. `go.sh build` fails on `uuid.New()` and the other random ids anywhere else.

@@go internal/shared/ids.go@@

`audit.go`:

@@go internal/shared/audit.go@@

`commandcontext.go`: the context a command carries. `FromEvent` is the context of a command sent in reaction to an event: same correlation and user, caused by the event.

@@go internal/shared/commandcontext.go@@

## Failures and validation

`errors.go`: every failure a caller can act on is an `*Error` with a stable code and a status. With `exceptions` = `minimal` nothing translates database errors: a duplicate key or a broken foreign key is a 500.

@@go internal/shared/errors.go@@

`validation.go`: a command checks its own shape in `Validate`; field names are the JSON names (PascalCase), nested ones `Lines[0].Sku`.

@@go internal/shared/validation.go@@

## Events and aggregates

`events.go`:

@@go internal/shared/events.go@@

`aggregate.go`: embedded in every aggregate. Creation sets both the created and the updated audit (the columns are not null).

@@go internal/shared/aggregate.go@@

`eventcontext.go`: for one top-level command and everything it causes: the stack of command contexts and the queues of raised and dispatched events. It travels in the `context.Context`.

@@go internal/shared/eventcontext.go@@

`handlers.go`: sync handlers run in the command's transaction, in sequence order, until no raised event is left; consumers run after the commit (`events-inprocess.md`).

@@go internal/shared/handlers.go@@

## The pipeline

`unitofwork.go`:

@@go internal/shared/unitofwork.go@@

`dispatcher.go`: one handler per command type, registered with `Register`; `Dispatch` completes the context, validates, runs the middlewares (logging), then the handler, inside `InTransaction` when the command embeds `shared.InTransaction`. Validation runs before the middlewares, so an invalid command logs nothing.

@@go internal/shared/dispatcher.go@@
