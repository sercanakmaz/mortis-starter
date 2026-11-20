# Logging, C#

Every project, every environment. Project `{Name}.Infrastructure`, folder `Logging/`. Layer 1: `request-lifecycle.md`.

Every log line is **one flat JSON object** on one line, `CorrelationId` first:

```json
{"CorrelationId":"01a1...","Timestamp":"2026-11-20T19:52:04.84Z","Level":"Information","Service":"Acme","Version":"1.0.0.0","Category":"Acme.Infrastructure.Logging.LoggingCommandHandlerMiddleware","Message":"CreateOrderCommand started","CausationId":null,"TenantId":null,"UserId":"u-1","Command":"CreateOrderCommand"}
```

- Fixed fields, in this order: `CorrelationId` (null outside a command or event), `Timestamp` (UTC), `Level`, `Service`, `Version`, `Category`, `Message`.
- Then every field of the open logging scopes and of the message template, at the top level: a command adds `CausationId`, `TenantId`, `UserId`, `Command`; an event being published or handled adds `CausationId`, `TenantId`, `UserId`, `Event`, `EventId`. An inner scope wins over an outer one.
- Then `Exception`, the exception as text, when there is one.
- No nesting: a value that is not a number or a boolean is written as a string.
- Log with message templates (`logger.LogInformation("{Command} started", name)`), never with built strings: the template's names become fields.

## LoggingConstants

File `Logging/LoggingConstants.cs`. `Service` is the project's `name`:

@@decl src/Boilerplate.Infrastructure/Logging/LoggingConstants.cs::LoggingConstants@@

@@members src/Boilerplate.Infrastructure/Logging/LoggingConstants.cs::LoggingConstants@@

## LogFields

File `Logging/LogFields.cs`, the scope fields of a command and of an event:

@@decl src/Boilerplate.Infrastructure/Logging/LogFields.cs::LogFields@@

@@members src/Boilerplate.Infrastructure/Logging/LogFields.cs::LogFields@@

## JsonLineFormatter

File `Logging/JsonLineFormatter.cs`, the console formatter that writes the line:

@@decl src/Boilerplate.Infrastructure/Logging/JsonLineFormatter.cs::JsonLineFormatter@@

@@member src/Boilerplate.Infrastructure/Logging/JsonLineFormatter.cs::JsonLineFormatter::Write@@

## LoggingModule

File `Logging/LoggingModule.cs`. Every host calls it first: the Api and the Worker with `builder.Logging.AddJsonLogging()`, the Console with `toStandardError: true`, so its stdout holds only the command's result.

@@decl src/Boilerplate.Infrastructure/Logging/LoggingModule.cs::LoggingModule@@

@@member src/Boilerplate.Infrastructure/Logging/LoggingModule.cs::LoggingModule::AddJsonLogging@@

`{Name}.Infrastructure.csproj` references `Microsoft.Extensions.Hosting` (pinned in `Directory.Packages.props`) for the console logger and, with an outbox, `BackgroundService`.

## LoggingCommandHandlerMiddleware

File `Logging/LoggingCommandHandlerMiddleware.cs`. Opens the command's scope, so every line logged while the command runs carries its fields:

@@decl src/Boilerplate.Infrastructure/Logging/LoggingCommandHandlerMiddleware.cs::LoggingCommandHandlerMiddleware@@

@@member src/Boilerplate.Infrastructure/Logging/LoggingCommandHandlerMiddleware.cs::LoggingCommandHandlerMiddleware::Before@@

@@member src/Boilerplate.Infrastructure/Logging/LoggingCommandHandlerMiddleware.cs::LoggingCommandHandlerMiddleware::After@@

- A nested command opens a nested scope; its fields win until it finishes.

## Tests

`JsonLineFormatterTests` in `testing-kernel.md`. The conformance suite reads the lines as JSON (`example-console.sh`).

## Namespaces and usings

Every file of this page, with its namespace and the `using` lines it needs (`ImplicitUsings` covers `System`, `System.Linq`, `System.Threading.Tasks` and the like). A file the table does not list follows the namespace rule in `naming.md`.

@@usings logging.md@@
