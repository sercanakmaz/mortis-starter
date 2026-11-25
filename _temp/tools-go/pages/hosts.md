# The hosts: bootstrap, API and Console

`internal/bootstrap` wires every layer once, for every host: no container, plain constructors.

@@go internal/bootstrap/bootstrap.go@@

## API

`internal/api/routes.go`: the standard library router. Each route builds its command, gives it the request's context and dispatches it. A malformed id in the path is a 404.

@@go internal/api/routes.go@@

`internal/api/respond.go`: results go out with camelCase members; failures as `{code, message, errors}`, with the field names of `errors` kept; an unexpected failure is a 500 with a reference that its log line repeats.

@@go internal/api/respond.go@@

Without authentication every request runs as an anonymous caller:

@@go internal/api/context.go@@

@@go cmd/api/main.go@@

## Console

`go run ./cmd/console -- --user <id> [--email <email>] <Command> [json | @file.json]` runs any registered command; `migrate` applies the migrations. Exit codes: 0 done, 1 usage or an unexpected failure, 2 a failure the caller can act on (printed on stderr as one JSON line with `code`).

@@go cmd/console/main.go@@

@@go cmd/console/arguments.go@@

@@go cmd/console/run.go@@

## Editor

`.vscode/launch.json` runs a command from the editor; every configuration passes `--user`.

@@go .vscode/launch.json@@
