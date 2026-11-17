# Hosts: Api and Console, C#

Layer 1: `layers.md` "Hosts", `building-blocks.md` "Host adapter". A host authenticates, fills the command's user, sends it through `ICommandDispatcher` and translates the outcome. A host never opens a transaction, never calls a repository and never migrates.

## Api

Project `{Name}.Api`.

### Program.cs

Top-level statements, the whole file:

```csharp
using {Name}.Api.Middlewares;
using {Name}.Api.Filters;
using {Name}.Infrastructure;
using {Name}.Application;
using {Name}.Domain;
using {Name}.Shared;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options => options.Filters.Add<ContextCommandFilter>());
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ExceptionHandler>();

builder.Services
    .AddSharedModule()
    .AddDomainModule()
    .AddApplicationModule()
    .AddInfrastructureModule(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();

app.MapOpenApi();
app.MapControllers();
app.MapGet("/health", () => Results.Ok());

app.Run();
```

### appsettings.json

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database={database};Username=postgres;Password=postgres"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  }
}
```

### Filling the context

File `Filters/ContextCommandFilter.cs`. Builds one context for the request, from the authenticated user's claims and the request's correlation id, and sets it on every `IContextCommand` argument, replacing whatever the client sent.

```csharp
public class ContextCommandFilter : IAsyncActionFilter
```

```csharp
public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
{
    var commandContext = new CommandContext
    {
        User = new AuditUser(context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), context.HttpContext.User.FindFirstValue(ClaimTypes.Email)),
        CorrelationId = context.HttpContext.Items[ApiConstants.CorrelationIdItem] is Guid correlationId ? correlationId : Guid.CreateVersion7()
    };

    foreach (var contextCommand in context.ActionArguments.Values.OfType<IContextCommand>())
    {
        contextCommand.Context = commandContext;
    }

    await next();
}
```

### Errors

File `Middlewares/ErrorResponseModel.cs`, the one error body:

```csharp
public record ErrorResponseModel(string Code, string Message, IDictionary<string, string[]> Errors = null);
```

File `Middlewares/ApiConstants.cs`:

```csharp
public static class ApiConstants
```

```csharp
public const string CorrelationIdHeader = "X-Correlation-Id";
```

```csharp
public const string CorrelationIdItem = "CorrelationId";
```

```csharp
public const string UnexpectedErrorCode = "unexpected_error";
```

```csharp
public const string UnexpectedErrorMessage = "An unexpected error occurred.";
```

File `Middlewares/ExceptionHandler.cs`:

```csharp
public class ExceptionHandler(ILogger<ExceptionHandler> logger) : IExceptionHandler
```

```csharp
public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
{
    var exceptionBase = exception as ExceptionBase;
    var httpStatusCode = exceptionBase?.HttpStatusCode ?? HttpStatusCode.InternalServerError;
    var code = exceptionBase?.Code ?? ApiConstants.UnexpectedErrorCode;
    var reference = ErrorReference.Of(exception);
    var isServerError = httpStatusCode >= HttpStatusCode.InternalServerError;

    logger.Log(isServerError ? LogLevel.Error : LogLevel.Warning, isServerError ? exception : null, "Request failed with {StatusCode} {Code} reference {Reference}", (int)httpStatusCode, code, reference);

    httpContext.Response.StatusCode = (int)httpStatusCode;

    await httpContext.Response.WriteAsJsonAsync(new ErrorResponseModel(
        code,
        exceptionBase?.Message ?? $"{ApiConstants.UnexpectedErrorMessage} Reference {reference}.",
        (exception as ValidationFailedException)?.Errors), cancellationToken);

    return true;
}
```

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

```csharp
[HttpPost]
public async Task<IActionResult> Create([FromBody] CreateOrderCommand command)
{
    var commandResult = await commandDispatcher.Send(command);

    return Created($"orders/{commandResult.Content.Id}", commandResult.Content);
}
```

```csharp
[HttpGet("{id:guid}")]
public async Task<IActionResult> Get([FromRoute] Guid id)
{
    var commandResult = await commandDispatcher.Send(new GetOrderCommand(id));

    return Ok(commandResult.Content);
}
```

An id from the route goes into the command with `with`:

```csharp
[HttpPut("{id:guid}/lines/{lineId:guid}/cancel")]
public async Task<IActionResult> CancelLine([FromRoute] Guid id, [FromRoute] Guid lineId, [FromBody] CancelOrderLineCommand command)
{
    var commandResult = await commandDispatcher.Send(command with { Id = id, LineId = lineId });

    return Ok(commandResult.Content);
}
```

An action with no body builds the command itself:

```csharp
[HttpPut("{id:guid}/complete")]
public async Task<IActionResult> Complete([FromRoute] Guid id)
{
    var commandResult = await commandDispatcher.Send(new CompleteOrderCommand(id));

    return Ok(commandResult.Content);
}
```

| Action | Verb and route | Answer |
|---|---|---|
| create | `POST /{plural}` | 201 with `Location` and the view |
| read one | `GET /{plural}/{id:guid}` | 200 with the view |
| an operation on one | `PUT /{plural}/{id:guid}/{operation}` | 200 with the view |
| add a child | `POST /{plural}/{id:guid}/{children}` | 200 with the parent's view |

## Console

Project `{Name}.Console`. Runs any registered command by name with a JSON argument, or a named scenario. Exit codes: 0 done, 1 unknown name, 2 a failure printed to stderr as `{ code, message, errors }`.

### Program.cs

```csharp
using Microsoft.Extensions.DependencyInjection;
using {Name}.Console.Scenarios;
using Microsoft.Extensions.Hosting;
using {Name}.Shared.Exceptions;
using System.Text.Json;
using {Name}.Infrastructure;
using {Name}.Application;
using {Name}.Domain;
using {Name}.Shared;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = args, ContentRootPath = AppContext.BaseDirectory });

builder.Services
    .AddSharedModule()
    .AddDomainModule()
    .AddApplicationModule()
    .AddInfrastructureModule(builder.Configuration)
    .AddScenariosModule();

using var host = builder.Build();
using var scope = host.Services.CreateScope();

var scenarios = scope.ServiceProvider.GetServices<IScenario>().ToList();
var scenario = scenarios.FirstOrDefault(candidate => string.Equals(candidate.Name, args.FirstOrDefault(), StringComparison.OrdinalIgnoreCase));

if (scenario is null)
{
    System.Console.WriteLine("Usage: dotnet run -- <scenario | command> [json | @file.json]");
    System.Console.WriteLine($"Names: {string.Join(", ", scenarios.Select(candidate => candidate.Name).Order())}");

    return 1;
}

System.Console.WriteLine($"correlation {ScenarioConstants.RunCorrelationId}");

try
{
    await scenario.RunAsync(args.Skip(1).ToArray());
}
catch (ExceptionBase exception)
{
    var errors = (exception as ValidationFailedException)?.Errors;

    System.Console.Error.WriteLine(JsonSerializer.Serialize(new { code = exception.Code, message = exception.Message, errors }, ScenarioConstants.OutputOptions));

    return 2;
}

return 0;
```

### appsettings.json

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database={database};Username=postgres;Password=postgres"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Hosting": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  }
}
```

### Scenarios

Folder `Scenarios/`, namespace `{Name}.Console.Scenarios`.

```csharp
public interface IScenario
```

```csharp
string Name { get; }
```

```csharp
Task RunAsync(string[] args);
```

```csharp
public static class ScenarioConstants
```

```csharp
public const string UserId = "console";
```

```csharp
public const string UserEmail = "console@localhost";
```

```csharp
public const string CommandSuffix = "Command";
```

```csharp
public const string FilePrefix = "@";
```

```csharp
public static readonly Guid RunCorrelationId = Guid.CreateVersion7();
```

```csharp
public static readonly JsonSerializerOptions OutputOptions = new() { WriteIndented = true };
```

```csharp
public static readonly JsonSerializerOptions InputOptions = new() { PropertyNameCaseInsensitive = true };
```

File `Scenarios/ConsoleUserExtensions.cs`:

```csharp
public static class ConsoleUserExtensions
```

```csharp
public static TCommand AsConsoleUser<TCommand>(this TCommand command)
{
    if (command is IContextCommand contextCommand)
    {
        contextCommand.Context = new CommandContext
        {
            User = new AuditUser(ScenarioConstants.UserId, ScenarioConstants.UserEmail),
            CorrelationId = ScenarioConstants.RunCorrelationId
        };
    }

    return command;
}
```

File `Scenarios/CommandScenario.cs`, the scenario that runs one command:

```csharp
public class CommandScenario<TCommand, TContent>(ICommandDispatcher commandDispatcher) : IScenario
    where TCommand : ICommand<TContent>
```

```csharp
public string Name => typeof(TCommand).Name[..^ScenarioConstants.CommandSuffix.Length];
```

```csharp
public async Task RunAsync(string[] args)
{
    var argument = args.FirstOrDefault();

    var json = string.IsNullOrWhiteSpace(argument) ? "{}"
        : argument.StartsWith(ScenarioConstants.FilePrefix) ? await File.ReadAllTextAsync(argument[ScenarioConstants.FilePrefix.Length..])
        : argument;

    TCommand command;

    try
    {
        command = JsonSerializer.Deserialize<TCommand>(json, ScenarioConstants.InputOptions);
    }
    catch (JsonException exception)
    {
        throw new InvalidParameterException("arguments", exception.Message, exception);
    }

    if (command is null)
    {
        throw new InvalidParameterException("arguments", "a JSON object is required");
    }

    var commandResult = await commandDispatcher.Send(command.AsConsoleUser());

    System.Console.WriteLine(JsonSerializer.Serialize(commandResult.Content, ScenarioConstants.OutputOptions));
}
```

File `Scenarios/CommandScenarioExtensions.cs`:

```csharp
public static class CommandScenarioExtensions
```

```csharp
public static IServiceCollection AddCommandScenario<TCommand, TContent>(this IServiceCollection services)
    where TCommand : ICommand<TContent>
{
    services.AddScoped<IScenario, CommandScenario<TCommand, TContent>>();

    return services;
}
```

File `Scenarios/ScenarioModule.cs`: one line per scenario class, then one `AddCommandScenario` line per command the Console may run, grouped by aggregate.

```csharp
public static class ScenarioModule
```

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

```json
{
  "version": "2.0.0",
  "tasks": [
    {
      "label": "build-console",
      "type": "process",
      "command": "dotnet",
      "args": ["build", "${workspaceFolder}/src/{Name}.Console/{Name}.Console.csproj", "--nologo"],
      "problemMatcher": "$msCompile"
    }
  ]
}
```

`.vscode/launch.json`. The first configuration asks for the name and the JSON; the others belong to the example and are left out when `example` is `false`.

```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": "Console: run a command or scenario (asks for the arguments)",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build-console",
      "program": "${workspaceFolder}/src/{Name}.Console/bin/Debug/net10.0/{Name}.Console.dll",
      "args": [
        "${input:name}",
        "${input:json}"
      ],
      "cwd": "${workspaceFolder}/src/{Name}.Console",
      "console": "integratedTerminal",
      "stopAtEntry": false
    },
    {
      "name": "Console: CreateProduct (edit the JSON in args)",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build-console",
      "program": "${workspaceFolder}/src/{Name}.Console/bin/Debug/net10.0/{Name}.Console.dll",
      "args": [
        "CreateProduct",
        "{\"sku\":\"BOLT\",\"name\":\"Bolt\",\"stock\":10}"
      ],
      "cwd": "${workspaceFolder}/src/{Name}.Console",
      "console": "integratedTerminal",
      "stopAtEntry": false
    },
    {
      "name": "Console: stock-flow scenario",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build-console",
      "program": "${workspaceFolder}/src/{Name}.Console/bin/Debug/net10.0/{Name}.Console.dll",
      "args": [
        "stock-flow"
      ],
      "cwd": "${workspaceFolder}/src/{Name}.Console",
      "console": "integratedTerminal",
      "stopAtEntry": false
    },
    {
      "name": "Console: order-flow scenario",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build-console",
      "program": "${workspaceFolder}/src/{Name}.Console/bin/Debug/net10.0/{Name}.Console.dll",
      "args": [
        "order-flow"
      ],
      "cwd": "${workspaceFolder}/src/{Name}.Console",
      "console": "integratedTerminal",
      "stopAtEntry": false
    }
  ],
  "inputs": [
    {
      "id": "name",
      "type": "promptString",
      "description": "A command name without its Command suffix, or a scenario name"
    },
    {
      "id": "json",
      "type": "promptString",
      "description": "The command as JSON, or @path/to/file.json. Leave empty for a scenario",
      "default": ""
    }
  ]
}
```

## Tests

`ContextCommandFilterTests` and `CommandScenarioTests` in `testing-kernel.md`.

## Namespaces and usings

Every file of this page, with its namespace and the `using` lines it needs (`ImplicitUsings` covers `System`, `System.Linq`, `System.Threading.Tasks` and the like). A file the table does not list follows the namespace rule in `naming.md`.

| File | Namespace | Usings |
|---|---|---|
| `src/{Name}.Api/Controllers/CategoriesController.cs` | `{Name}.Api.Controllers` | Microsoft.AspNetCore.Mvc, {Name}.Application.Categories, {Name}.Shared.Application |
| `src/{Name}.Api/Controllers/CustomersController.cs` | `{Name}.Api.Controllers` | Microsoft.AspNetCore.Mvc, {Name}.Application.Customers, {Name}.Shared.Application |
| `src/{Name}.Api/Controllers/OrdersController.cs` | `{Name}.Api.Controllers` | Microsoft.AspNetCore.Mvc, {Name}.Application.Orders, {Name}.Shared.Application |
| `src/{Name}.Api/Controllers/ProductsController.cs` | `{Name}.Api.Controllers` | Microsoft.AspNetCore.Mvc, {Name}.Application.Products, {Name}.Shared.Application |
| `src/{Name}.Api/Filters/ContextCommandFilter.cs` | `{Name}.Api.Filters` | Microsoft.AspNetCore.Mvc.Filters, System.Security.Claims, {Name}.Api.Middlewares, {Name}.Shared.Application, {Name}.Shared.Domain |
| `src/{Name}.Api/Middlewares/ApiConstants.cs` | `{Name}.Api.Middlewares` | none |
| `src/{Name}.Api/Middlewares/ErrorResponseModel.cs` | `{Name}.Api.Middlewares` | none |
| `src/{Name}.Api/Middlewares/ExceptionHandler.cs` | `{Name}.Api.Middlewares` | Microsoft.AspNetCore.Diagnostics, System.Net, {Name}.Shared.Exceptions |
| `src/{Name}.Api/Program.cs` | `` | {Name}.Api.Filters, {Name}.Api.Middlewares, {Name}.Application, {Name}.Domain, {Name}.Infrastructure, {Name}.Shared |
| `src/{Name}.Console/Program.cs` | `` | Microsoft.Extensions.DependencyInjection, Microsoft.Extensions.Hosting, System.Text.Json, var host = builder.Build(), var scope = host.Services.CreateScope(), {Name}.Application, {Name}.Console.Scenarios, {Name}.Domain, {Name}.Infrastructure, {Name}.Shared, {Name}.Shared.Exceptions |
| `src/{Name}.Console/Scenarios/BlockedCustomerScenario.cs` | `{Name}.Console.Scenarios` | System.Text.Json, {Name}.Application.Customers, {Name}.Application.Orders, {Name}.Application.Products, {Name}.Domain.Aggregates.Orders, {Name}.Shared.Application, {Name}.Shared.Exceptions |
| `src/{Name}.Console/Scenarios/CommandScenario.cs` | `{Name}.Console.Scenarios` | System.Text.Json, {Name}.Shared.Application, {Name}.Shared.Exceptions |
| `src/{Name}.Console/Scenarios/CommandScenarioExtensions.cs` | `{Name}.Console.Scenarios` | Microsoft.Extensions.DependencyInjection, {Name}.Shared.Application |
| `src/{Name}.Console/Scenarios/ConsoleUserExtensions.cs` | `{Name}.Console.Scenarios` | {Name}.Shared.Application, {Name}.Shared.Domain |
| `src/{Name}.Console/Scenarios/IScenario.cs` | `{Name}.Console.Scenarios` | none |
| `src/{Name}.Console/Scenarios/OrderFlowScenario.cs` | `{Name}.Console.Scenarios` | System.Text.Json, {Name}.Application.Customers, {Name}.Application.Orders, {Name}.Application.Products, {Name}.Domain.Aggregates.Orders, {Name}.Shared.Application |
| `src/{Name}.Console/Scenarios/ScenarioConstants.cs` | `{Name}.Console.Scenarios` | System.Text.Json |
| `src/{Name}.Console/Scenarios/ScenarioModule.cs` | `{Name}.Console.Scenarios` | Microsoft.Extensions.DependencyInjection, {Name}.Application.Categories, {Name}.Application.Customers, {Name}.Application.Orders, {Name}.Application.Products |
| `src/{Name}.Console/Scenarios/StockFlowScenario.cs` | `{Name}.Console.Scenarios` | System.Text.Json, {Name}.Application.Customers, {Name}.Application.Orders, {Name}.Application.Products, {Name}.Domain.Aggregates.Orders, {Name}.Shared.Application |
