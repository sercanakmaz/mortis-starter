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

```csharp
public static class LoggingConstants
```

```csharp
public static class LoggingConstants
```

```csharp
public const string FormatterName = "json-line";
```

```csharp
public const string Service = "{Name}";
```

```csharp
public const string CorrelationId = "CorrelationId";
```

```csharp
public const string OriginalFormat = "{OriginalFormat}";
```

## LogFields

File `Logging/LogFields.cs`, the scope fields of a command and of an event:

```csharp
public static class LogFields
```

```csharp
public static class LogFields
```

```csharp
public static Dictionary<string, object> Of(ICommand command)
{
    var commandContext = (command as IContextCommand)?.Context;

    return new Dictionary<string, object>
    {
        [LoggingConstants.CorrelationId] = commandContext?.CorrelationId,
        ["CausationId"] = commandContext?.CausationId,
        ["TenantId"] = commandContext?.TenantId,
        ["UserId"] = commandContext?.User?.Id,
        ["Language"] = commandContext?.Language,
        ["Command"] = command.GetType().Name
    };
}
```

```csharp
public static Dictionary<string, object> Of(EventBase @event)
{
    return new Dictionary<string, object>
    {
        [LoggingConstants.CorrelationId] = @event.CorrelationId,
        ["CausationId"] = @event.CausationId,
        ["TenantId"] = @event.TenantId,
        ["UserId"] = @event.UserId,
        ["Language"] = @event.Language,
        ["Event"] = @event.GetType().Name,
        ["EventId"] = @event.EventId
    };
}
```

## JsonLineFormatter

File `Logging/JsonLineFormatter.cs`, the console formatter that writes the line:

```csharp
public sealed class JsonLineFormatter() : ConsoleFormatter(LoggingConstants.FormatterName)
```

```csharp
public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider scopeProvider, TextWriter textWriter)
{
    var fields = new Dictionary<string, object>();

    scopeProvider?.ForEachScope((scope, collected) =>
    {
        if (scope is not IEnumerable<KeyValuePair<string, object>> pairs) return;

        foreach (var pair in pairs.Where(pair => pair.Key != LoggingConstants.OriginalFormat))
        {
            collected[pair.Key] = pair.Value;
        }
    }, fields);

    if (logEntry.State is IEnumerable<KeyValuePair<string, object>> statePairs)
    {
        foreach (var pair in statePairs.Where(pair => pair.Key != LoggingConstants.OriginalFormat))
        {
            fields[pair.Key] = pair.Value;
        }
    }

    using var stream = new MemoryStream();

    using (var writer = new Utf8JsonWriter(stream))
    {
        writer.WriteStartObject();
        writer.WriteString(LoggingConstants.CorrelationId, Convert.ToString(fields.GetValueOrDefault(LoggingConstants.CorrelationId), CultureInfo.InvariantCulture) is { Length: > 0 } correlationId ? correlationId : null);
        writer.WriteString("Timestamp", DateTime.UtcNow);
        writer.WriteString("Level", logEntry.LogLevel.ToString());
        writer.WriteString("Service", LoggingConstants.Service);
        writer.WriteString("Version", Version);
        writer.WriteString("Category", logEntry.Category);
        writer.WriteString("Message", logEntry.Formatter(logEntry.State, logEntry.Exception));

        foreach (var (key, value) in fields.Where(field => field.Key != LoggingConstants.CorrelationId))
        {
            if (value is null)
            {
                writer.WriteNull(key);
            }
            else if (value is bool or int or long or double or decimal)
            {
                writer.WritePropertyName(key);
                JsonSerializer.Serialize(writer, value);
            }
            else
            {
                writer.WriteString(key, Convert.ToString(value, CultureInfo.InvariantCulture));
            }
        }
```

The member continues:

```csharp
        if (logEntry.Exception is not null)
        {
            writer.WriteString("Exception", logEntry.Exception.ToString());
        }

        writer.WriteEndObject();
    }

    textWriter.WriteLine(Encoding.UTF8.GetString(stream.ToArray()));
}
```

## LoggingModule

File `Logging/LoggingModule.cs`. Every host calls it first: the Api and the Worker with `builder.Logging.AddJsonLogging()`, the Console with `toStandardError: true`, so its stdout holds only the command's result.

```csharp
public static class LoggingModule
```

```csharp
public static ILoggingBuilder AddJsonLogging(this ILoggingBuilder logging, bool toStandardError = false)
{
    logging.ClearProviders();
    logging.AddConsole(options =>
    {
        options.FormatterName = LoggingConstants.FormatterName;

        if (toStandardError)
        {
            options.LogToStandardErrorThreshold = LogLevel.Trace;
        }
    });
    logging.AddConsoleFormatter<JsonLineFormatter, ConsoleFormatterOptions>();

    return logging;
}
```

`{Name}.Infrastructure.csproj` references `Microsoft.Extensions.Hosting` (pinned in `Directory.Packages.props`) for the console logger and, with an outbox, `BackgroundService`.

## LoggingCommandHandlerMiddleware

File `Logging/LoggingCommandHandlerMiddleware.cs`. Opens the command's scope, so every line logged while the command runs carries its fields:

```csharp
public class LoggingCommandHandlerMiddleware(ILogger<LoggingCommandHandlerMiddleware> logger) : ICommandHandlerMiddleware
```

```csharp
public void Before(ICommand command)
{
    _scopes.Push(logger.BeginScope(LogFields.Of(command)));

    logger.LogInformation("{Command} started", command.GetType().Name);
}
```

```csharp
public void After(ICommand command, CommandResult commandResult)
{
    logger.LogInformation("{Command} finished", command.GetType().Name);

    if (_scopes.TryPop(out var scope))
    {
        scope.Dispose();
    }
}
```

- A nested command opens a nested scope; its fields win until it finishes.

## Tests

`JsonLineFormatterTests` in `testing-kernel.md`. The conformance suite reads the lines as JSON (`example-console.sh`).

## Namespaces and usings

Every file of this page, with its namespace and the `using` lines it needs (`ImplicitUsings` covers `System`, `System.Linq`, `System.Threading.Tasks` and the like). A file the table does not list follows the namespace rule in `naming.md`.

| File | Namespace | Usings |
|---|---|---|
| `src/{Name}.Infrastructure/Logging/JsonLineFormatter.cs` | `{Name}.Infrastructure.Logging` | Microsoft.Extensions.Logging, Microsoft.Extensions.Logging.Abstractions, Microsoft.Extensions.Logging.Console, System.Globalization, System.Reflection, System.Text, System.Text.Json |
| `src/{Name}.Infrastructure/Logging/LogFields.cs` | `{Name}.Infrastructure.Logging` | {Name}.Shared.Application, {Name}.Shared.Domain |
| `src/{Name}.Infrastructure/Logging/LoggingCommandHandlerMiddleware.cs` | `{Name}.Infrastructure.Logging` | Microsoft.Extensions.Logging, {Name}.Shared.Application |
| `src/{Name}.Infrastructure/Logging/LoggingConstants.cs` | `{Name}.Infrastructure.Logging` | none |
| `src/{Name}.Infrastructure/Logging/LoggingModule.cs` | `{Name}.Infrastructure.Logging` | Microsoft.Extensions.Logging, Microsoft.Extensions.Logging.Console |
