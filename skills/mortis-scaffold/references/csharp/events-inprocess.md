# Publishing events directly, C#

Applies when `events.outbox` is `none`. Project `{Name}.Infrastructure`, folder `Messaging/`. Layer 1: `events.md`.

Two classes. After `COMMIT` the unit of work hands each queue event to `EventPublisher`, which logs it and runs its async handlers in a new scope, each in its own transaction, as the system user. Nothing is stored.

What can be lost: an event whose async handler had not run when the process died. Say so plainly when it matters; the outbox options remove it.

With an outbox, neither class exists in the project (`outbox.md`).

## EventPublisher

File `Messaging/EventPublisher.cs`, registered as a singleton by `AddInfrastructureModule`:

```csharp
public class EventPublisher(IServiceScopeFactory serviceScopeFactory, ILogger<EventPublisher> logger)
```

```csharp
public async Task PublishAsync(EventBase @event)
{
    using var logScope = logger.BeginScope(LogFields.Of(@event));
    using var scope = serviceScopeFactory.CreateScope();

    logger.LogInformation("Published {Event} {EventId}", @event.GetType().Name, @event.EventId);

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

                logger.LogError(exception, "Async handler {EventHandler} failed; the transaction that raised the event stays committed", eventHandler.GetType().Name);

                break;
            }
        }
    }
}
```

- One scope for the event; one transaction per handler, in `Sequence` order.
- A concurrency conflict is retried in a fresh transaction up to `MaxAttempts`; any other failure is logged and the next handler still runs.
- Its log lines carry the event's fields (`LogFields.Of(event)`, `logging.md`): `CorrelationId`, `CausationId`, `TenantId`, `UserId`, `Event`, `EventId`.
- An async handler that writes a contended row needs the `xmin` row version on that aggregate (`infrastructure-postgres.md`).

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

## The unit of work's part

`UnitOfWork` (`infrastructure-postgres.md`) takes `EventPublisher` and, after `COMMIT`, publishes each queue event; a failure is logged and never undoes the commit:

```csharp
public async Task CommitTransactionAsync(string transactionKey)
{
    if (_transaction is null || transactionKey != _transactionKey) return;

    await SaveChangesAsync();

    await eventHandlerExecutor.ExecuteRaisedAsync();

    var queueEvents = new List<EventBase>();

    while (eventContext.TakeDispatched() is { } @event)
    {
        if (@event is IQueueEvent) queueEvents.Add(@event);
    }

    await _transaction.CommitAsync();
    await _transaction.DisposeAsync();

    _transaction = null;
    _transactionKey = null;

    foreach (var queueEvent in queueEvents)
    {
        try
        {
            await eventPublisher.PublishAsync(queueEvent);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Publishing {Event} {EventId} failed after the transaction was committed", queueEvent.GetType().Name, queueEvent.EventId);
        }
    }

    var committedActions = _committedActions.ToList();

    _committedActions.Clear();

    foreach (var committedAction in committedActions)
    {
        await committedAction();
    }
}
```

## Tests

File `test/{Name}.Tests/Infrastructure/Messaging/EventPublisherTests.cs`, with `EventPublisherBuilder` in `TestSupport/Infrastructure/Messaging/`:

```csharp
public class EventPublisherTests
```

```csharp
[Fact]
public async Task PublishAsync_WithTwoHandlers_Should_RunEachInItsOwnTransactionInSequenceOrder()
{
    var calls = new List<string>();
    var eventPublisher = EventPublisherBuilder.Create()
        .WithUnitOfWork(UnitOfWorkBuilder.Create().WithRecordedCalls(calls).Build())
        .WithEventHandler(new RecordingAsyncEventHandler<FakeQueueEvent>("second", 2, calls))
        .WithEventHandler(new RecordingAsyncEventHandler<FakeQueueEvent>("first", 1, calls))
        .Build();

    await eventPublisher.PublishAsync(new FakeQueueEvent("event"));

    Assert.Equal(["BeginTransactionAsync", "first", "CommitTransactionAsync", "BeginTransactionAsync", "second", "CommitTransactionAsync"], calls);
}
```

```csharp
[Fact]
public async Task PublishAsync_WhenAHandlerThrows_Should_RollBackLogAndStillRunTheNextHandler()
{
    var calls = new List<string>();
    var eventPublisherBuilder = EventPublisherBuilder.Create()
        .WithUnitOfWork(UnitOfWorkBuilder.Create().WithRecordedCalls(calls).Build())
        .WithEventHandler(new RecordingAsyncEventHandler<FakeQueueEvent>("failing", 1, calls, 1, new InvalidOperationException("boom")))
        .WithEventHandler(new RecordingAsyncEventHandler<FakeQueueEvent>("healthy", 2, calls));
    var eventPublisher = eventPublisherBuilder.Build();

    await eventPublisher.PublishAsync(new FakeQueueEvent("event"));

    Assert.Equal(["BeginTransactionAsync", "failing", "RollbackTransactionAsync", "BeginTransactionAsync", "healthy", "CommitTransactionAsync"], calls);
    var entry = Assert.Single(eventPublisherBuilder.BuildLogger().Entries, logEntry => logEntry.Level == LogLevel.Error);
    Assert.IsType<InvalidOperationException>(entry.Exception);
}
```

```csharp
[Fact]
public async Task PublishAsync_WhenTheHandlerHitsAConcurrencyConflict_Should_RetryInAFreshTransaction()
{
    var calls = new List<string>();
    var eventPublisherBuilder = EventPublisherBuilder.Create()
        .WithUnitOfWork(UnitOfWorkBuilder.Create().WithRecordedCalls(calls).Build())
        .WithEventHandler(new RecordingAsyncEventHandler<FakeQueueEvent>("contended", 0, calls, 2, new DbUpdateConcurrencyException("stale")));
    var eventPublisher = eventPublisherBuilder.Build();

    await eventPublisher.PublishAsync(new FakeQueueEvent("event"));

    Assert.Equal(
        ["BeginTransactionAsync", "contended", "RollbackTransactionAsync", "BeginTransactionAsync", "contended", "RollbackTransactionAsync", "BeginTransactionAsync", "contended", "CommitTransactionAsync"],
        calls);
    Assert.DoesNotContain(eventPublisherBuilder.BuildLogger().Entries, logEntry => logEntry.Level == LogLevel.Error);
}
```

```csharp
[Fact]
public async Task PublishAsync_WhenTheConflictNeverEnds_Should_GiveUpAfterTheMaximumAttemptsAndLog()
{
    var calls = new List<string>();
    var eventPublisherBuilder = EventPublisherBuilder.Create()
        .WithUnitOfWork(UnitOfWorkBuilder.Create().WithRecordedCalls(calls).Build())
        .WithEventHandler(new RecordingAsyncEventHandler<FakeQueueEvent>("contended", 0, calls, int.MaxValue, new DbUpdateConcurrencyException("stale")));
    var eventPublisher = eventPublisherBuilder.Build();

    await eventPublisher.PublishAsync(new FakeQueueEvent("event"));

    Assert.Equal(MessagingConstants.MaxAttempts, calls.Count(call => call == "contended"));
    Assert.Equal(MessagingConstants.MaxAttempts, calls.Count(call => call == "RollbackTransactionAsync"));
    Assert.Single(eventPublisherBuilder.BuildLogger().Entries, logEntry => logEntry.Level == LogLevel.Error);
}
```

```csharp
[Fact]
public async Task PublishAsync_WithoutHandlersForTheEvent_Should_NotOpenATransaction()
{
    var calls = new List<string>();
    var eventPublisher = EventPublisherBuilder.Create()
        .WithUnitOfWork(UnitOfWorkBuilder.Create().WithRecordedCalls(calls).Build())
        .Build();

    await eventPublisher.PublishAsync(new FakeQueueEvent("event"));

    Assert.Empty(calls);
}
```

```csharp
public class EventPublisherBuilder
```

```csharp
private readonly ServiceCollection _services = new();
```

```csharp
private readonly RecordingLogger<EventPublisher> _logger = new();
```

```csharp
public static EventPublisherBuilder Create() => new();
```

```csharp
public EventPublisherBuilder WithUnitOfWork(IUnitOfWork unitOfWork)
{
    _services.AddScoped(_ => unitOfWork);
    return this;
}
```

```csharp
public EventPublisherBuilder WithEventHandler<TEvent>(BaseAsyncEventHandler<TEvent> eventHandler) where TEvent : EventBase, IQueueEvent
{
    _services.AddSingleton(eventHandler);
    return this;
}
```

```csharp
public EventPublisherBuilder WithScopedEventHandler<TEvent>(Func<IServiceProvider, BaseAsyncEventHandler<TEvent>> factory) where TEvent : EventBase, IQueueEvent
{
    _services.AddScoped(factory);
    return this;
}
```

```csharp
public RecordingLogger<EventPublisher> BuildLogger() => _logger;
```

```csharp
public EventPublisher Build()
{
    var serviceProvider = _services.BuildServiceProvider();

    return new EventPublisher(serviceProvider.GetRequiredService<IServiceScopeFactory>(), _logger);
}
```

## Namespaces and usings

Every file of this page, with its namespace and the `using` lines it needs (`ImplicitUsings` covers `System`, `System.Linq`, `System.Threading.Tasks` and the like). A file the table does not list follows the namespace rule in `naming.md`.

| File | Namespace | Usings |
|---|---|---|
| `src/{Name}.Infrastructure/Messaging/EventPublisher.cs` | `{Name}.Infrastructure.Messaging` | Microsoft.EntityFrameworkCore, Microsoft.Extensions.DependencyInjection, Microsoft.Extensions.Logging, {Name}.Infrastructure.Logging, {Name}.Shared.Domain, {Name}.Shared.Persistence |
| `src/{Name}.Infrastructure/Messaging/MessagingConstants.cs` | `{Name}.Infrastructure.Messaging` | none |
| `test/{Name}.Tests/Infrastructure/Messaging/EventPublisherTests.cs` | `{Name}.Tests.Infrastructure.Messaging` | Microsoft.EntityFrameworkCore, Microsoft.Extensions.DependencyInjection, Microsoft.Extensions.Logging, {Name}.Infrastructure.Messaging, {Name}.Shared.Domain, {Name}.Tests.TestSupport.Infrastructure.Messaging, {Name}.Tests.TestSupport.Shared.Domain, {Name}.Tests.TestSupport.Shared.Persistence |
| `test/{Name}.Tests/TestSupport/Infrastructure/Messaging/EventPublisherBuilder.cs` | `{Name}.Tests.TestSupport.Infrastructure.Messaging` | Microsoft.Extensions.DependencyInjection, {Name}.Infrastructure.Messaging, {Name}.Shared.Domain, {Name}.Shared.Persistence |
