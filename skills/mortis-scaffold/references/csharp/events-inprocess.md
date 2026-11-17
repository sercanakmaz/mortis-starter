# Events in process, C#

Applies when `events.outbox` is `none` and `events.transport` is `in-process`. Project `{Name}.Infrastructure`, folder `Messaging/`. Layer 1: `events.md`.

Committed queue events go to every registered `IEventDispatcher`. Two are registered, in this order: `LoggingEventDispatcher` writes the wire name to the log; `AsyncEventHandlerDispatcher` runs the async handlers in a new scope, each in its own transaction, as the system user.

What can be lost: an event whose async handler had not run when the process died. Say so plainly when it matters; the outbox options remove it.

## MessagingConstants

File `Messaging/MessagingConstants.cs`:

```csharp
public static class MessagingConstants
```

```csharp
public const int MaxAttempts = 10;
```

```csharp
public const int RetryDelayMilliseconds = 20;
```

## LoggingEventDispatcher

File `Messaging/LoggingEventDispatcher.cs`:

```csharp
public class LoggingEventDispatcher(ILogger<LoggingEventDispatcher> logger) : IEventDispatcher
```

```csharp
public Task Publish(EventBase @event)
{
    var eventType = @event.GetType();

    logger.LogInformation("Published {Exchange} {EventId} correlation {CorrelationId} causation {CausationId}", $"{eventType.Namespace}:{eventType.Name}", @event.EventId, @event.CorrelationId, @event.CausationId);

    return Task.CompletedTask;
}
```

- The wire name is `<namespace>:<type>`, for example `{Name}.Domain.Aggregates.Orders.Events:Completed`.

## AsyncEventHandlerDispatcher

File `Messaging/AsyncEventHandlerDispatcher.cs`:

```csharp
public class AsyncEventHandlerDispatcher(IServiceScopeFactory serviceScopeFactory, ILogger<AsyncEventHandlerDispatcher> logger) : IEventDispatcher
```

```csharp
public async Task Publish(EventBase @event)
{
    if (@event is not IQueueEvent) return;

    using var scope = serviceScopeFactory.CreateScope();

    var eventHandlers = scope.ServiceProvider
        .GetServices(typeof(BaseAsyncEventHandler<>).MakeGenericType(@event.GetType()))
        .Cast<IAsyncEventHandler>()
        .OrderBy(eventHandler => eventHandler.Sequence)
        .ToList();

    if (eventHandlers.Count == 0) return;

    var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

    foreach (var eventHandler in eventHandlers)
    {
        var transactionKey = $"{@event.EventId}:{eventHandler.GetType().Name}";

        for (var attempt = 1; attempt <= MessagingConstants.MaxAttempts; attempt++)
        {
            try
            {
                await unitOfWork.BeginTransactionAsync(transactionKey);
                await eventHandler.Handle(@event);
                await unitOfWork.CommitTransactionAsync(transactionKey);

                break;
            }
            catch (DbUpdateConcurrencyException) when (attempt < MessagingConstants.MaxAttempts)
            {
                await unitOfWork.RollbackTransactionAsync();
                await Task.Delay(TimeSpan.FromMilliseconds(attempt * MessagingConstants.RetryDelayMilliseconds));
            }
            catch (Exception exception)
            {
                await unitOfWork.RollbackTransactionAsync();

                logger.LogError(exception, "Async handler {EventHandler} failed for {EventType} {EventId} correlation {CorrelationId}; the transaction that raised the event stays committed", eventHandler.GetType().Name, @event.GetType().FullName, @event.EventId, @event.CorrelationId);

                break;
            }
        }
    }
}
```

- One scope for the event; one transaction per handler.
- A concurrency conflict is retried in a fresh transaction up to `MaxAttempts`; any other failure is logged and the next handler still runs.
- An async handler that writes a contended row needs the `xmin` row version on that aggregate (`infrastructure-postgres.md`).

## Tests

`AsyncEventHandlerDispatcherTests` in `testing-kernel.md`.

## Namespaces and usings

Every file of this page, with its namespace and the `using` lines it needs (`ImplicitUsings` covers `System`, `System.Linq`, `System.Threading.Tasks` and the like). A file the table does not list follows the namespace rule in `naming.md`.

| File | Namespace | Usings |
|---|---|---|
| `src/{Name}.Infrastructure/Messaging/AsyncEventHandlerDispatcher.cs` | `{Name}.Infrastructure.Messaging` | Microsoft.EntityFrameworkCore, Microsoft.Extensions.DependencyInjection, Microsoft.Extensions.Logging, {Name}.Shared.Domain, {Name}.Shared.Persistence |
| `src/{Name}.Infrastructure/Messaging/LoggingEventDispatcher.cs` | `{Name}.Infrastructure.Messaging` | Microsoft.Extensions.Logging, {Name}.Shared.Domain |
| `src/{Name}.Infrastructure/Messaging/MessagingConstants.cs` | `{Name}.Infrastructure.Messaging` | none |
