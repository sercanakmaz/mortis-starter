# Hosts: Api, Console and Worker, C#

Layer 1: `layers.md` "Hosts", `building-blocks.md` "Host adapter". A host authenticates, fills the command's user, sends it through `ICommandDispatcher` and translates the outcome. A host never opens a transaction, never calls a repository and never migrates.

## Api

Project `{Name}.Api`.

### Program.cs

Top-level statements, the whole file:

@@data src/Boilerplate.Api/Program.cs csharp@@

The `AddEventConsumer` lines are there with `events.transport` = `in-process` or `database`, one per event consumer; with `rabbitmq` the Worker has them instead and this file has none (`event-consumers.md`).

### appsettings.json

@@data src/Boilerplate.Api/appsettings.json json@@

With an outbox and `hosts.worker` = `in-api`, the Api also runs the outbox worker: one more line after `.AddInfrastructureModule(builder.Configuration)`, `.AddOutboxWorker()` (`outbox.md`).

### Filling the context

File `Filters/ContextCommandFilter.cs`. Builds one context for the request, from the authenticated user's claims and the request's correlation id, and sets it on every `IContextCommand` argument, replacing whatever the client sent.

@@decl src/Boilerplate.Api/Filters/ContextCommandFilter.cs::ContextCommandFilter@@

@@member src/Boilerplate.Api/Filters/ContextCommandFilter.cs::ContextCommandFilter::OnActionExecutionAsync@@

### Errors

File `Middlewares/ErrorResponseModel.cs`, the one error body:

@@decl src/Boilerplate.Api/Middlewares/ErrorResponseModel.cs::ErrorResponseModel@@

File `Middlewares/ApiConstants.cs`:

@@decl src/Boilerplate.Api/Middlewares/ApiConstants.cs::ApiConstants@@

@@member src/Boilerplate.Api/Middlewares/ApiConstants.cs::ApiConstants::CorrelationIdHeader@@

@@member src/Boilerplate.Api/Middlewares/ApiConstants.cs::ApiConstants::CorrelationIdItem@@

@@member src/Boilerplate.Api/Middlewares/ApiConstants.cs::ApiConstants::UnexpectedErrorCode@@

@@member src/Boilerplate.Api/Middlewares/ApiConstants.cs::ApiConstants::UnexpectedErrorMessage@@

File `Middlewares/ExceptionHandler.cs`:

@@decl src/Boilerplate.Api/Middlewares/ExceptionHandler.cs::ExceptionHandler@@

@@member src/Boilerplate.Api/Middlewares/ExceptionHandler.cs::ExceptionHandler::TryHandleAsync@@

It uses `ErrorReference` (`failures.md`), written at every level.

- Every failure answers `{ code, message, errors }` with the exception's status; `errors` is set only for `validation_failed`. Anything that is not an `ExceptionBase` answers 500 `unexpected_error` with a reference, never its own message. 4xx is logged as a warning, 5xx as an error with the exception. The table of statuses is in `failures.md`.

### Correlation

The header middleware belongs to the `correlation` add-on (`correlation.md`). Without it, `ContextCommandFilter` makes a new correlation id per request.

### Controllers

File `Controllers/{Plural}Controller.cs`, one per aggregate. Each action sends one command and returns its content. The route follows `naming.routes`.

```csharp
[ApiController]
[Route("orders")]
public class OrdersController(ICommandDispatcher commandDispatcher) : ControllerBase
```

@@member src/Boilerplate.Api/Controllers/OrdersController.cs::OrdersController::Create@@

@@member src/Boilerplate.Api/Controllers/OrdersController.cs::OrdersController::Get@@

An id from the route goes into the command with `with`:

@@member src/Boilerplate.Api/Controllers/OrdersController.cs::OrdersController::CancelLine@@

An action with no body builds the command itself:

@@member src/Boilerplate.Api/Controllers/OrdersController.cs::OrdersController::Complete@@

| Action | Verb and route | Answer |
|---|---|---|
| create | `POST /{plural}` | 201 with `Location` and the view |
| read one | `GET /{plural}/{id:guid}` | 200 with the view |
| an operation on one | `PUT /{plural}/{id:guid}/{operation}` | 200 with the view |
| add a child | `POST /{plural}/{id:guid}/{children}` | 200 with the parent's view |

## Console

Project `{Name}.Console`. A host that runs one command and exits: `dotnet run -- <Command> [json | @file.json]`. No scenarios: a sequence of commands is a sequence of runs.

- The command is found by name in the Application assembly (`<Name>Command`, case ignored), read from the JSON (names ignore case), sent through `ICommandDispatcher` as the console user, with a new correlation id per run.
- stdout holds only the result, as indented JSON; logs go to stderr as JSON lines (`logging.md`), so the result can be piped.
- Exit codes: 0 done, 1 no command name, 2 a failure printed to stderr as one line `{ code, message, errors }`.
- With `events.outbox` = `background` the Console only writes outbox rows; the worker in the Api or the Worker publishes them.

### Program.cs

@@data src/Boilerplate.Console/Program.cs csharp@@

The `AddEventConsumer` lines are there with `events.transport` = `in-process` or `database`, one per event consumer; with `rabbitmq` the Worker has them instead and this file has none (`event-consumers.md`).

### appsettings.json

@@data src/Boilerplate.Console/appsettings.json json@@

### ConsoleCommandRunner

File `ConsoleCommandRunner.cs`, namespace `{Name}.Console`:

@@decl src/Boilerplate.Console/ConsoleCommandRunner.cs::ConsoleCommandRunner@@

@@member src/Boilerplate.Console/ConsoleCommandRunner.cs::ConsoleCommandRunner::RunAsync@@

- A new command needs nothing in the Console: the runner finds it.

File `ConsoleConstants.cs`:

@@decl src/Boilerplate.Console/ConsoleConstants.cs::ConsoleConstants@@

@@members src/Boilerplate.Console/ConsoleConstants.cs::ConsoleConstants@@

## VS Code

`.vscode/tasks.json`:

@@data .vscode/tasks.json json@@

`.vscode/launch.json`. The first configuration asks for the name and the JSON; the second belongs to the example and is left out when `example` is `false`.

@@data .vscode/launch.json json@@

## Tests

`ContextCommandFilterTests` and `ConsoleCommandRunnerTests` in `testing-kernel.md`.

## Namespaces and usings

Every file of this page, with its namespace and the `using` lines it needs (`ImplicitUsings` covers `System`, `System.Linq`, `System.Threading.Tasks` and the like). A file the table does not list follows the namespace rule in `naming.md`.

@@usings hosts.md@@
