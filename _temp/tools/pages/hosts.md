# Hosts: Api and Console, C#

Layer 1: `layers.md` "Hosts", `building-blocks.md` "Host adapter". A host authenticates, fills the command's user, sends it through `ICommandDispatcher` and translates the outcome. A host never opens a transaction, never calls a repository and never migrates.

## Api

Project `{Name}.Api`.

### Program.cs

Top-level statements, the whole file:

@@data src/Boilerplate.Api/Program.cs csharp@@

### appsettings.json

@@data src/Boilerplate.Api/appsettings.json json@@

### Filling the user

File `Filters/UserCommandFilter.cs`. Copies the authenticated user's claims into every `IUserCommand` argument, and clears what the client sent when nobody is signed in.

@@decl src/Boilerplate.Api/Filters/UserCommandFilter.cs::UserCommandFilter@@

@@member src/Boilerplate.Api/Filters/UserCommandFilter.cs::UserCommandFilter::OnActionExecutionAsync@@

### Errors

File `Middlewares/ErrorResponseModel.cs`, the one error body:

@@decl src/Boilerplate.Api/Middlewares/ErrorResponseModel.cs::ErrorResponseModel@@

File `Middlewares/ApiConstants.cs`:

@@decl src/Boilerplate.Api/Middlewares/ApiConstants.cs::ApiConstants@@

@@member src/Boilerplate.Api/Middlewares/ApiConstants.cs::ApiConstants::CorrelationIdHeader@@

@@member src/Boilerplate.Api/Middlewares/ApiConstants.cs::ApiConstants::UnexpectedErrorCode@@

@@member src/Boilerplate.Api/Middlewares/ApiConstants.cs::ApiConstants::UnexpectedErrorMessage@@

File `Middlewares/ExceptionHandler.cs`:

@@decl src/Boilerplate.Api/Middlewares/ExceptionHandler.cs::ExceptionHandler@@

@@member src/Boilerplate.Api/Middlewares/ExceptionHandler.cs::ExceptionHandler::TryHandleAsync@@

- Every failure answers `{ code, message, errors }` with the exception's status; `errors` is set only for `validation_failed`. Anything that is not an `ExceptionBase` answers 500 `unexpected_error` and is logged.

### Correlation

File `Middlewares/CorrelationIdMiddleware.cs`:

@@decl src/Boilerplate.Api/Middlewares/CorrelationIdMiddleware.cs::CorrelationIdMiddleware@@

@@member src/Boilerplate.Api/Middlewares/CorrelationIdMiddleware.cs::CorrelationIdMiddleware::InvokeAsync@@

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

Project `{Name}.Console`. Runs any registered command by name with a JSON argument, or a named scenario. Exit codes: 0 done, 1 unknown name, 2 a failure printed to stderr as `{ code, message, errors }`.

### Program.cs

@@data src/Boilerplate.Console/Program.cs csharp@@

### appsettings.json

@@data src/Boilerplate.Console/appsettings.json json@@

### Scenarios

Folder `Scenarios/`, namespace `{Name}.Console.Scenarios`.

@@decl src/Boilerplate.Console/Scenarios/IScenario.cs::IScenario@@

@@member src/Boilerplate.Console/Scenarios/IScenario.cs::IScenario::Name@@

@@member src/Boilerplate.Console/Scenarios/IScenario.cs::IScenario::RunAsync@@

@@decl src/Boilerplate.Console/Scenarios/ScenarioConstants.cs::ScenarioConstants@@

@@member src/Boilerplate.Console/Scenarios/ScenarioConstants.cs::ScenarioConstants::UserId@@

@@member src/Boilerplate.Console/Scenarios/ScenarioConstants.cs::ScenarioConstants::UserEmail@@

@@member src/Boilerplate.Console/Scenarios/ScenarioConstants.cs::ScenarioConstants::CommandSuffix@@

@@member src/Boilerplate.Console/Scenarios/ScenarioConstants.cs::ScenarioConstants::FilePrefix@@

@@member src/Boilerplate.Console/Scenarios/ScenarioConstants.cs::ScenarioConstants::OutputOptions@@

@@member src/Boilerplate.Console/Scenarios/ScenarioConstants.cs::ScenarioConstants::InputOptions@@

File `Scenarios/ConsoleUserExtensions.cs`:

@@decl src/Boilerplate.Console/Scenarios/ConsoleUserExtensions.cs::ConsoleUserExtensions@@

@@member src/Boilerplate.Console/Scenarios/ConsoleUserExtensions.cs::ConsoleUserExtensions::AsConsoleUser@@

File `Scenarios/CommandScenario.cs`, the scenario that runs one command:

@@decl src/Boilerplate.Console/Scenarios/CommandScenario.cs::CommandScenario@@

@@member src/Boilerplate.Console/Scenarios/CommandScenario.cs::CommandScenario::Name@@

@@member src/Boilerplate.Console/Scenarios/CommandScenario.cs::CommandScenario::RunAsync@@

File `Scenarios/CommandScenarioExtensions.cs`:

@@decl src/Boilerplate.Console/Scenarios/CommandScenarioExtensions.cs::CommandScenarioExtensions@@

@@member src/Boilerplate.Console/Scenarios/CommandScenarioExtensions.cs::CommandScenarioExtensions::AddCommandScenario@@

File `Scenarios/ScenarioModule.cs`: one line per scenario class, then one `AddCommandScenario` line per command the Console may run, grouped by aggregate.

@@decl src/Boilerplate.Console/Scenarios/ScenarioModule.cs::ScenarioModule@@

```csharp
public static IServiceCollection AddScenariosModule(this IServiceCollection services)
{
    services.AddScoped<IScenario, OrderFlowScenario>();

    services.AddCommandScenario<CreateOrderCommand, OrderDto>();
    services.AddCommandScenario<GetOrderCommand, OrderDto>();

    return services;
}
```

Written first with an empty body (only `return services;`), and lines added with the example or each new aggregate.

## VS Code

`.vscode/tasks.json`:

@@data .vscode/tasks.json json@@

`.vscode/launch.json`. The first configuration asks for the name and the JSON; the others belong to the example and are left out when `example` is `false`.

@@data .vscode/launch.json json@@

## Tests

`UserCommandFilterTests` and `CommandScenarioTests` in `testing-kernel.md`.

## Namespaces and usings

Every file of this page, with its namespace and the `using` lines it needs (`ImplicitUsings` covers `System`, `System.Linq`, `System.Threading.Tasks` and the like). A file the table does not list follows the namespace rule in `naming.md`.

@@usings hosts.md@@
