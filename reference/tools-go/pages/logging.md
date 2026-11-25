# Logging: internal/infrastructure/logging

`log/slog` with one handler that writes every record as one flat JSON line on stderr, `CorrelationId` first. The fields of a scope (a command, an event) travel in the `context.Context`; every log call passes `ctx`.

@@go internal/infrastructure/logging/constants.go@@

@@go internal/infrastructure/logging/fields.go@@

@@go internal/infrastructure/logging/json_line_handler.go@@

`command_middleware.go`: the dispatcher's one middleware. `"<Command> started"` is the line the conformance checks look for.

@@go internal/infrastructure/logging/command_middleware.go@@
