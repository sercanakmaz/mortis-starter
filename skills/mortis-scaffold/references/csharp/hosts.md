# Hosts: Api, Console and Worker, C#

Layer 1: `layers.md` "Hosts", `building-blocks.md` "Host adapter". A host authenticates, fills the command's user, sends it through `ICommandDispatcher` and translates the outcome. A host never opens a transaction, never calls a repository and never migrates.

## Api

Project `{Name}.Api`.

### Program.cs

Top-level statements, the whole file:

```csharp
using {Name}.Api.Middlewares;
using {Name}.Api.Filters;
using {Name}.Infrastructure.Logging;
using {Name}.Infrastructure.Messaging;
using {Name}.Application.Products;
using {Name}.Infrastructure;
using {Name}.Application;
using {Name}.Domain;
using {Name}.Shared;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddJsonLogging();

builder.Services.AddControllers(options => options.Filters.Add<ContextCommandFilter>());
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ExceptionHandler>();

builder.Services
    .AddSharedModule()
    .AddDomainModule()
    .AddApplicationModule()
    .AddInfrastructureModule(builder.Configuration)
    .AddEventConsumer<DecreaseStockWhenOrderLineAddedEventConsumer>(endpoint => endpoint.Retry(3))
    .AddEventConsumer<IncreaseStockWhenOrderLineCancelledEventConsumer>(endpoint => endpoint.Retry(3));

var app = builder.Build();

app.UseExceptionHandler();

app.MapOpenApi();
app.MapControllers();
app.MapGet("/health", () => Results.Ok());

app.Run();
```

The `AddEventConsumer` lines are there with `events.transport` = `in-process` or `database`, one per event consumer; with `rabbitmq` the Worker has them instead and this file has none (`event-consumers.md`).

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

With an outbox and `hosts.worker` = `in-api`, the Api also runs the outbox worker: one more line after `.AddInfrastructureModule(builder.Configuration)`, `.AddOutboxWorker()` (`outbox.md`).

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

Project `{Name}.Console`. A host that runs one command and exits: `dotnet run -- <Command> [json | @file.json]`. No scenarios: a sequence of commands is a sequence of runs.

- The command is found by name in the Application assembly (`<Name>Command`, case ignored), read from the JSON (names ignore case), sent through `ICommandDispatcher` as the console user, with a new correlation id per run.
- stdout holds only the result, as indented JSON; logs go to stderr as JSON lines (`logging.md`), so the result can be piped.
- Exit codes: 0 done, 1 no command name, 2 a failure printed to stderr as one line `{ code, message, errors }`.
- With `events.outbox` = `background` the Console only writes outbox rows; the worker in the Api or the Worker publishes them.

### Program.cs

```csharp
using Microsoft.Extensions.DependencyInjection;
using {Name}.Infrastructure.Logging;
using Microsoft.Extensions.Hosting;
using {Name}.Shared.Application;
using {Name}.Shared.Exceptions;
using {Name}.Infrastructure.Messaging;
using {Name}.Application.Products;
using {Name}.Infrastructure;
using {Name}.Application;
using {Name}.Console;
using {Name}.Domain;
using {Name}.Shared;
using System.Text.Json;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = args, ContentRootPath = AppContext.BaseDirectory });

builder.Logging.AddJsonLogging(toStandardError: true);

builder.Services
    .AddSharedModule()
    .AddDomainModule()
    .AddApplicationModule()
    .AddInfrastructureModule(builder.Configuration)
    .AddEventConsumer<DecreaseStockWhenOrderLineAddedEventConsumer>(endpoint => endpoint.Retry(3))
    .AddEventConsumer<IncreaseStockWhenOrderLineCancelledEventConsumer>(endpoint => endpoint.Retry(3));

using var host = builder.Build();

if (args.Length == 0)
{
    System.Console.Error.WriteLine("Usage: dotnet run -- <Command> [json | @file.json]");

    return 1;
}

using var scope = host.Services.CreateScope();

var consoleCommandRunner = new ConsoleCommandRunner(scope.ServiceProvider.GetRequiredService<ICommandDispatcher>(), typeof(ApplicationModule).Assembly);

try
{
    var content = await consoleCommandRunner.RunAsync(args[0], args.ElementAtOrDefault(1));

    System.Console.WriteLine(JsonSerializer.Serialize(content, ConsoleConstants.OutputOptions));

    return 0;
}
catch (ExceptionBase exception)
{
    var errors = (exception as ValidationFailedException)?.Errors;

    System.Console.Error.WriteLine(JsonSerializer.Serialize(new { code = exception.Code, message = exception.Message, errors }));

    return 2;
}
```

The `AddEventConsumer` lines are there with `events.transport` = `in-process` or `database`, one per event consumer; with `rabbitmq` the Worker has them instead and this file has none (`event-consumers.md`).

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

### ConsoleCommandRunner

File `ConsoleCommandRunner.cs`, namespace `{Name}.Console`:

```csharp
public class ConsoleCommandRunner(ICommandDispatcher commandDispatcher, Assembly commandAssembly)
```

```csharp
public async Task<object> RunAsync(string name, string argument)
{
    var commandType = commandAssembly.GetTypes()
        .FirstOrDefault(type => !type.IsAbstract && type.Name.Equals(name + ConsoleConstants.CommandSuffix, StringComparison.OrdinalIgnoreCase));

    var contentType = commandType?.GetInterfaces()
        .FirstOrDefault(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(ICommand<>))?
        .GetGenericArguments()[0]
        ?? throw new InvalidParameterException("command", $"{name} is not a command");

    var json = string.IsNullOrWhiteSpace(argument) ? "{}"
        : argument.StartsWith(ConsoleConstants.FilePrefix) ? await File.ReadAllTextAsync(argument[ConsoleConstants.FilePrefix.Length..])
        : argument;

    object command;

    try
    {
        command = JsonSerializer.Deserialize(json, commandType, ConsoleConstants.InputOptions);
    }
    catch (JsonException exception)
    {
        throw new InvalidParameterException("arguments", exception.Message, exception);
    }

    if (command is null)
    {
        throw new InvalidParameterException("arguments", "a JSON object is required");
    }

    if (command is IContextCommand contextCommand)
    {
        contextCommand.Context = new CommandContext { User = new AuditUser(ConsoleConstants.UserId, ConsoleConstants.UserEmail) };
    }

    var send = typeof(ICommandDispatcher).GetMethod(nameof(ICommandDispatcher.Send)).MakeGenericMethod(contentType);
    dynamic commandResult = await (dynamic)send.Invoke(commandDispatcher, [command]);

    return commandResult.Content;
}
```

- A new command needs nothing in the Console: the runner finds it.

File `ConsoleConstants.cs`:

```csharp
public static class ConsoleConstants
```

```csharp
public static class ConsoleConstants
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
public static readonly JsonSerializerOptions OutputOptions = new() { WriteIndented = true };
```

```csharp
public static readonly JsonSerializerOptions InputOptions = new() { PropertyNameCaseInsensitive = true };
```

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

`.vscode/launch.json`. The first configuration asks for the name and the JSON; the second belongs to the example and is left out when `example` is `false`.

```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": "Console: run a command (asks for the name and the JSON)",
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
    }
  ],
  "inputs": [
    {
      "id": "name",
      "type": "promptString",
      "description": "A command name without its Command suffix"
    },
    {
      "id": "json",
      "type": "promptString",
      "description": "The command as JSON, or @path/to/file.json. Leave empty for a command without fields",
      "default": ""
    }
  ]
}
```

## Tests

`ContextCommandFilterTests` and `ConsoleCommandRunnerTests` in `testing-kernel.md`.

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
| `src/{Name}.Api/Program.cs` | `` | {Name}.Api.Filters, {Name}.Api.Middlewares, {Name}.Application, {Name}.Application.Products, {Name}.Domain, {Name}.Infrastructure, {Name}.Infrastructure.Logging, {Name}.Infrastructure.Messaging, {Name}.Shared |
| `src/{Name}.Console/ConsoleCommandRunner.cs` | `{Name}.Console` | System.Reflection, System.Text.Json, {Name}.Shared.Application, {Name}.Shared.Domain, {Name}.Shared.Exceptions |
| `src/{Name}.Console/ConsoleConstants.cs` | `{Name}.Console` | System.Text.Json |
| `src/{Name}.Console/Program.cs` | `` | Microsoft.Extensions.DependencyInjection, Microsoft.Extensions.Hosting, System.Text.Json, {Name}.Application, {Name}.Application.Products, {Name}.Console, {Name}.Domain, {Name}.Infrastructure, {Name}.Infrastructure.Logging, {Name}.Infrastructure.Messaging, {Name}.Shared, {Name}.Shared.Application, {Name}.Shared.Exceptions |
