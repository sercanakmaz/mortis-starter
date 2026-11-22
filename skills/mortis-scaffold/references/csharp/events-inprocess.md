# Publishing events directly, C#

Applies when `events.outbox` is `none`. Project `{Name}.Infrastructure`, folder `Messaging/`. Layer 1: `events.md`.

Two classes. After `COMMIT` the unit of work hands each queue event to `EventPublisher`, which logs it and runs the event's registered consumers (`event-consumers.md`), each in its own scope and transaction, as the user whose command raised the event. Nothing is stored.

What can be lost: an event whose event consumer had not run when the process died. Say so plainly when it matters; the outbox options remove it.

With an outbox, neither class exists in the project (`outbox.md`).

## EventPublisher

File `Messaging/EventPublisher.cs`, registered as a singleton by `AddInfrastructureModule`:

```csharp
public class EventPublisher(IServiceScopeFactory serviceScopeFactory, EventConsumerRegistry eventConsumerRegistry, ILogger<EventPublisher> logger)
```

```csharp
public async Task PublishAsync(EventBase @event)
{
    using var logScope = logger.BeginScope(LogFields.Of(@event));

    logger.LogInformation("Published {Event} {EventId}", @event.GetType().Name, @event.EventId);

    foreach (var eventConsumerEndpoint in eventConsumerRegistry.For(@event.GetType()))
    {
        await ConsumeAsync(eventConsumerEndpoint, @event);
    }
}
```

- Every event consumer is a consumer of its own: a fresh scope and transaction per attempt, in the order of the `AddEventConsumer` lines.
- A failure, a concurrency conflict included, is retried after a short delay up to the consumer's `Retry(n)`; after the last attempt it is logged as an error and the next consumer still runs.
- Its log lines carry the event's fields (`LogFields.Of(event)`, `logging.md`): `CorrelationId`, `CausationId`, `TenantId`, `UserId`, `Event`, `EventId`.
- An event consumer that writes a contended row needs the `xmin` row version on that aggregate (`infrastructure-postgres.md`).

## MessagingConstants

File `Messaging/MessagingConstants.cs`. Only the delay between attempts; how many attempts is each consumer's `Retry(n)`:

```csharp
public static class MessagingConstants
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
public async Task PublishAsync_WithTwoConsumers_Should_RunEachInItsOwnTransactionInRegistrationOrder()
{
    var calls = new List<string>();
    var eventPublisher = EventPublisherBuilder.Create()
        .WithUnitOfWork(UnitOfWorkBuilder.Create().WithRecordedCalls(calls).Build())
        .WithEventConsumer(new OtherRecordingEventConsumer(calls), 0)
        .WithEventConsumer(new RecordingEventConsumer(calls), 0)
        .Build();

    await eventPublisher.PublishAsync(new FakeQueueEvent("event"));

    Assert.Equal(["BeginTransactionAsync", "OtherRecordingEventConsumer", "CommitTransactionAsync", "BeginTransactionAsync", "RecordingEventConsumer", "CommitTransactionAsync"], calls);
}
```

```csharp
[Fact]
public async Task PublishAsync_WhenAConsumerFails_Should_RetryItInAFreshTransactionUpToItsRetryCount()
{
    var calls = new List<string>();
    var eventPublisherBuilder = EventPublisherBuilder.Create()
        .WithUnitOfWork(UnitOfWorkBuilder.Create().WithRecordedCalls(calls).Build())
        .WithEventConsumer(new RecordingEventConsumer(calls, 2, new InvalidOperationException("boom")), 2);
    var eventPublisher = eventPublisherBuilder.Build();

    await eventPublisher.PublishAsync(new FakeQueueEvent("event"));

    Assert.Equal(
        ["BeginTransactionAsync", "RecordingEventConsumer", "RollbackTransactionAsync", "BeginTransactionAsync", "RecordingEventConsumer", "RollbackTransactionAsync", "BeginTransactionAsync", "RecordingEventConsumer", "CommitTransactionAsync"],
        calls);
    Assert.DoesNotContain(eventPublisherBuilder.BuildLogger().Entries, logEntry => logEntry.Level == LogLevel.Error);
}
```

```csharp
[Fact]
public async Task PublishAsync_WhenAConsumerKeepsFailing_Should_GiveUpAfterItsRetriesLogAndStillRunTheNextConsumer()
{
    var calls = new List<string>();
    var eventPublisherBuilder = EventPublisherBuilder.Create()
        .WithUnitOfWork(UnitOfWorkBuilder.Create().WithRecordedCalls(calls).Build())
        .WithEventConsumer(new RecordingEventConsumer(calls, int.MaxValue, new InvalidOperationException("boom")), 1)
        .WithEventConsumer(new OtherRecordingEventConsumer(calls), 0);
    var eventPublisher = eventPublisherBuilder.Build();

    await eventPublisher.PublishAsync(new FakeQueueEvent("event"));

    Assert.Equal(
        ["BeginTransactionAsync", "RecordingEventConsumer", "RollbackTransactionAsync", "BeginTransactionAsync", "RecordingEventConsumer", "RollbackTransactionAsync", "BeginTransactionAsync", "OtherRecordingEventConsumer", "CommitTransactionAsync"],
        calls);
    var entry = Assert.Single(eventPublisherBuilder.BuildLogger().Entries, logEntry => logEntry.Level == LogLevel.Error);
    Assert.IsType<InvalidOperationException>(entry.Exception);
}
```

```csharp
[Fact]
public async Task PublishAsync_WithoutConsumersForTheEvent_Should_NotOpenATransaction()
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
public EventPublisherBuilder WithEventConsumer<TConsumer>(TConsumer eventConsumer, int retryCount) where TConsumer : class, IEventConsumer
{
    _services.AddEventConsumer<TConsumer>(endpoint => endpoint.Retry(retryCount));
    _services.AddSingleton(eventConsumer);
    return this;
}
```

```csharp
public RecordingLogger<EventPublisher> BuildLogger() => _logger;
```

```csharp
public EventPublisher Build()
{
    _services.AddEventConsumerRegistry();

    var serviceProvider = _services.BuildServiceProvider();

    return new EventPublisher(serviceProvider.GetRequiredService<IServiceScopeFactory>(), serviceProvider.GetRequiredService<EventConsumerRegistry>(), _logger);
}
```

## Namespaces and usings

Every file of this page, with its namespace and the `using` lines it needs (`ImplicitUsings` covers `System`, `System.Linq`, `System.Threading.Tasks` and the like). A file the table does not list follows the namespace rule in `naming.md`.

| File | Namespace | Usings |
|---|---|---|
| `src/{Name}.Infrastructure/Messaging/EventPublisher.cs` | `{Name}.Infrastructure.Messaging` | Microsoft.Extensions.DependencyInjection, Microsoft.Extensions.Logging, {Name}.Infrastructure.Logging, {Name}.Shared.Domain, {Name}.Shared.Persistence |
| `src/{Name}.Infrastructure/Messaging/MessagingConstants.cs` | `{Name}.Infrastructure.Messaging` | none |
| `test/{Name}.Tests/Infrastructure/Messaging/EventPublisherTests.cs` | `{Name}.Tests.Infrastructure.Messaging` | Microsoft.Extensions.Logging, {Name}.Tests.TestSupport.Infrastructure.Messaging, {Name}.Tests.TestSupport.Shared.Domain, {Name}.Tests.TestSupport.Shared.Persistence |
| `test/{Name}.Tests/TestSupport/Infrastructure/Messaging/EventPublisherBuilder.cs` | `{Name}.Tests.TestSupport.Infrastructure.Messaging` | Microsoft.Extensions.DependencyInjection, {Name}.Infrastructure.Messaging, {Name}.Shared.Domain, {Name}.Shared.Persistence |
