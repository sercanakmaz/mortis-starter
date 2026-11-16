# Hosts: Api and Console

Both hosts wire the same modules (`AddSharedModule()`, `AddDomainModule()`, `AddApplicationModule()`, `AddInfrastructureModule(configuration)`). Neither holds logic.

## Api (`src/Api/`)

| File | What it does |
|---|---|
| `Program.cs` | Registers controllers with `UserCommandFilter`, the exception handler, the modules; maps `/health` and OpenAPI. |
| `Controllers/<Plural>Controller.cs` | Example only. Binds a body to a command and sends it through `ICommandDispatcher`: `Created(...)` for a create, `Ok(...)` otherwise. A route id is applied with `command with { Id = id }`. |
| `Filters/UserCommandFilter.cs` | Fills `UserId` and `UserEmail` of every `IUserCommand` argument from the `NameIdentifier` and `Email` claims, and clears anything the client sent. |
| `Middlewares/ExceptionHandler.cs` | `ExceptionBase` → `{ code, message, errors }` with its HTTP status; anything else → 500 and a log line. |
| `Middlewares/CorrelationIdMiddleware.cs` | Reads or creates the correlation id that events and logs carry. |
| `Middlewares/ErrorResponseModel.cs`, `ApiConstants.cs` | The error shape and its constants. |

Authentication is not configured, so until it is the audit user of an Api write is empty.

## Console (`src/Console/`)

The Console exists so you can run a command by hand. It never migrates; use `dotnet ef database update`.

```bash
dotnet run --project src/<Name>.Console --                                   # lists every name
dotnet run --project src/<Name>.Console -- CreateProduct '{"sku":"BOLT","name":"Bolt","stock":10}'
dotnet run --project src/<Name>.Console -- CreateProduct @product.json       # JSON from a file
```

| File | What it does |
|---|---|
| `Program.cs` | Builds the host, finds a scenario by name (case-insensitive), runs it. An unknown name exits with 1; an `ExceptionBase` prints `{ code, message, errors }` to stderr and exits with 2. |
| `Scenarios/IScenario.cs` | `Name` and `RunAsync(string[] args)`. |
| `Scenarios/CommandScenario.cs` | One generic scenario per command: reads the first argument as JSON (case-insensitive names, empty means defaults), sends the command as the console user, prints the content. Its name is the command's type name without `Command`. |
| `Scenarios/CommandScenarioExtensions.cs` | `AddCommandScenario<TCommand, TContent>()`. |
| `Scenarios/ConsoleUserExtensions.cs` | `.AsConsoleUser()` sets `console` on an `IUserCommand`. |
| `Scenarios/ScenarioConstants.cs` | The console user, JSON options and prefixes. |
| `Scenarios/ScenarioModule.cs` | **You decide what the Console can run**: one line per command, plus hand-written scenarios. |

A hand-written scenario (the example has `order-flow`, `stock-flow`, `blocked-customer`) is for a manual test that needs several steps; it sends commands and prints JSON. Logs and the printed JSON share stdout.

## VS Code

`.vscode/launch.json` has a configuration that asks for the name and the JSON each time you press F5, and fixed ones you can copy and edit (`args` holds the name, then the JSON). `.vscode/tasks.json` builds the Console first. The example adds ready-made entries for `CreateProduct`, `stock-flow` and `order-flow`.
