# The outbox, C#

Applies when `events.outbox` is `background` (publish in the command) or `after-command` (publish after the command). Project `{Name}.Infrastructure`, folder `Messaging/Outbox/`; the Worker project when `hosts.worker` is `separate`. Layer 1: `events.md` "The three publishing models", `options/events.md`.

Generate only the chosen mode. The files below are the `background` code; a box marked **after-command** lists the only additions of that mode. With `none` nothing on this page exists (`events-inprocess.md`).

## How it fits together

```
command -> unitOfWork.CommitTransactionAsync
  save -> sync handlers -> collect the queue events
  outboxRepository.Add(events)          one row per event and registered consumer, in the SAME transaction
  save, COMMIT
  after-command only: outboxWorker.PublishAsync(id) for each of its rows

OutboxWorker (hosted, every PollingIntervalMilliseconds)
  repeat until nothing is due:
    new scope, BEGIN
    claim the next due row ("for update skip locked")
    run the row's consumer in this transaction
    delete the row, COMMIT
    on failure: ROLLBACK, count the attempt, wait 2^attempts seconds
```

- Every event consumer is a consumer of its own: it gets its own row, transaction, retries and failure. A failing consumer never blocks or repeats another consumer of the same event.
- One row, its consumer and its deletion are one transaction: a row is either fully handled and gone, or untouched and retried. No inbox is needed.
- An event without a registered consumer gets no row. Consumers are registered in the hosts (`event-consumers.md`).
- `skip locked` lets two workers, or a request and a worker, run side by side; each row goes to one of them.
- Each row carries `max_attempts`, its consumer's `Retry(n)` + 1. A row that reaches it stays in the table for an operator. To retry it: `update outbox_messages set attempts = 0, next_attempt_at = now() where id = '<id>'`.
- Rows are taken in `occurred_at, id` order; a retried row can come after a later one.

## The files

| File | Job |
|---|---|
| `OutboxMessage.cs` | The row: the event as JSON, the consumer that takes it, its attempts and attempt limit, when to try next, the last error |
| `OutboxMessageConfiguration.cs` | Its table, `outbox_messages` |
| `OutboxRepository.cs` | Add a row per event and registered consumer, claim the next due row, delete it, record a failure |
| `OutboxWorker.cs` | The background loop that publishes, sweeps and retries |
| `OutboxSettings.cs` | The polling interval (and, with `rabbitmq`, the longest wait between publish attempts) |
| `OutboxModule.cs` | The registrations |

## OutboxMessage

File `Messaging/Outbox/OutboxMessage.cs`:

```csharp
public class OutboxMessage
```


```csharp
public Guid Id { get; private set; }
```

```csharp
public Guid EventId { get; private set; }
```

```csharp
public string Consumer { get; private set; }
```

```csharp
public string Type { get; private set; }
```

```csharp
public string Payload { get; private set; }
```

```csharp
public DateTime OccurredAt { get; private set; }
```

```csharp
public int Attempts { get; private set; }
```

```csharp
public int MaxAttempts { get; private set; }
```

```csharp
public DateTime NextAttemptAt { get; private set; }
```

```csharp
public string LastError { get; private set; }
```

```csharp
public static OutboxMessage From(EventBase @event, EventConsumerEndpoint eventConsumerEndpoint)
{
    return new OutboxMessage
    {
        Id = Guid.CreateVersion7(),
        EventId = @event.EventId,
        Consumer = eventConsumerEndpoint.ConsumerType.FullName,
        Type = @event.GetType().FullName,
        Payload = JsonSerializer.Serialize(@event, @event.GetType()),
        OccurredAt = @event.OccurredAt,
        MaxAttempts = eventConsumerEndpoint.RetryCount + 1,
        NextAttemptAt = @event.OccurredAt
    };
}
```

```csharp
public EventBase ToEvent(Assembly eventAssembly)
{
    return (EventBase)JsonSerializer.Deserialize(Payload, eventAssembly.GetType(Type, throwOnError: true));
}
```

- `EventId` is the event's id, `Consumer` the full name of the consumer that takes this row, `MaxAttempts` its `Retry(n)` + 1. The correlation, causation, tenant and user travel inside the payload, as they do on the event.
- With `events.transport` = `rabbitmq` the row has no `EventId`, `Consumer` or `MaxAttempts`: there is one row per event, its id is the event id, the broker fans it out to one queue per consumer, and publishing is retried until the broker takes it (`rabbitmq.md`).
- `Type` is the event's full type name, read back against the Domain assembly. Renaming or moving an event type strands its pending rows: publish them first.

File `Messaging/Outbox/OutboxMessageConfiguration.cs` (picked up by `ApplyConfigurationsFromAssembly`):

```csharp
public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
```

```csharp
public void Configure(EntityTypeBuilder<OutboxMessage> builder)
{
    builder.HasKey(message => message.Id);
    builder.Property(message => message.Id).ValueGeneratedNever();
    builder.Property(message => message.Consumer).HasMaxLength(300).IsRequired();
    builder.Property(message => message.Type).HasMaxLength(300).IsRequired();
    builder.Property(message => message.Payload).HasColumnType("jsonb").IsRequired();

    builder.HasIndex(message => message.NextAttemptAt);
    builder.HasIndex(message => message.EventId);

    builder.ToTable("outbox_messages");
}
```

## OutboxRepository

File `Messaging/Outbox/OutboxRepository.cs`, scoped, on the request's `AppDbContext`:

```csharp
public class OutboxRepository(AppDbContext context, EventConsumerRegistry eventConsumerRegistry)
```


```csharp
public void Add(IEnumerable<EventBase> events)
{
    foreach (var @event in events)
    {
        context.Set<OutboxMessage>().AddRange(eventConsumerRegistry.For(@event.GetType()).Select(eventConsumerEndpoint => OutboxMessage.From(@event, eventConsumerEndpoint)));
    }
}
```

```csharp
public async Task<OutboxMessage> ClaimNextAsync()
{
    var messages = await context.Set<OutboxMessage>()
        .FromSql($"select * from outbox_messages where attempts < max_attempts and next_attempt_at <= {DateTime.UtcNow} order by occurred_at, id limit 1 for update skip locked")
        .ToListAsync();

    return messages.SingleOrDefault();
}
```

```csharp
public async Task<OutboxMessage> ClaimAsync(Guid eventId)
{
    var messages = await context.Set<OutboxMessage>()
        .FromSql($"select * from outbox_messages where event_id = {eventId} and attempts = 0 order by id limit 1 for update skip locked")
        .ToListAsync();

    return messages.SingleOrDefault();
}
```

```csharp
public void Remove(OutboxMessage message)
{
    context.Set<OutboxMessage>().Remove(message);
}
```

```csharp
public async Task RecordFailureAsync(OutboxMessage message, Exception exception)
{
    context.ChangeTracker.Clear();

    await context.Set<OutboxMessage>().Where(row => row.Id == message.Id).ExecuteUpdateAsync(row => row
        .SetProperty(column => column.Attempts, message.Attempts + 1)
        .SetProperty(column => column.NextAttemptAt, DateTime.UtcNow.AddSeconds(Math.Pow(2, message.Attempts)))
        .SetProperty(column => column.LastError, exception.Message));
}
```

- `Add` only tracks the rows; the unit of work saves them before `COMMIT`.
- The raw SQL uses the snake_case names of `naming.database`.

**after-command** adds one member, to claim the event's next fresh row (a row that already failed is left to the worker's backoff):

```csharp
public async Task<OutboxMessage> ClaimAsync(Guid eventId)
{
    var messages = await context.Set<OutboxMessage>()
        .FromSql($"select * from outbox_messages where event_id = {eventId} and attempts = 0 order by id limit 1 for update skip locked")
        .ToListAsync();

    return messages.SingleOrDefault();
}
```

## OutboxWorker

File `Messaging/Outbox/OutboxWorker.cs`. This is the `database` transport, which runs the consumers itself; with `events.transport` = `rabbitmq` the worker publishes the row instead (`rabbitmq.md`).

```csharp
public class OutboxWorker(IServiceScopeFactory serviceScopeFactory, EventConsumerRegistry eventConsumerRegistry, IOptions<OutboxSettings> settings, ILogger<OutboxWorker> logger) : BackgroundService
```

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        try
        {
            while (await PublishAsync(outboxRepository => outboxRepository.ClaimNextAsync()))
            {
            }
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogError(exception, "The outbox worker failed; it tries again after the polling interval");
        }

        await Task.Delay(settings.Value.PollingIntervalMilliseconds, stoppingToken);
    }
}
```

```csharp
private async Task<bool> PublishAsync(Func<OutboxRepository, Task<OutboxMessage>> claim)
{
    using var scope = serviceScopeFactory.CreateScope();
    var outboxRepository = scope.ServiceProvider.GetRequiredService<OutboxRepository>();
    var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

    await unitOfWork.BeginTransactionAsync(nameof(OutboxWorker));

    var message = await claim(outboxRepository);

    if (message is null)
    {
        await unitOfWork.RollbackTransactionAsync();

        return false;
    }

    var @event = message.ToEvent(typeof(DomainModule).Assembly);

    using var logScope = logger.BeginScope(LogFields.Of(@event));

    try
    {
        var eventConsumer = (IEventConsumer)scope.ServiceProvider.GetRequiredService(eventConsumerRegistry.ByConsumer(message.Consumer).ConsumerType);

        await eventConsumer.Handle(@event);

        outboxRepository.Remove(message);

        await unitOfWork.CommitTransactionAsync(nameof(OutboxWorker));

        logger.LogInformation("Handled {Event} {EventId} by {EventConsumer}", @event.GetType().Name, @event.EventId, message.Consumer);
    }
    catch (Exception exception)
    {
        await unitOfWork.RollbackTransactionAsync();
        await outboxRepository.RecordFailureAsync(message, exception);

        logger.LogWarning(exception, "Event consumer {EventConsumer} failed for {Event} {EventId}, attempt {Attempt}", message.Consumer, @event.GetType().Name, @event.EventId, message.Attempts + 1);
    }

    return true;
}
```

- Its log lines carry the event's fields (`logging.md`): `CorrelationId`, `CausationId`, `TenantId`, `UserId`, `Event`, `EventId`.
- The consumers' commands join the worker's transaction; events they raise become rows in it.

**after-command** adds one member, which the unit of work calls right after `COMMIT`; it handles the event's rows one consumer at a time:

```csharp
public async Task PublishAsync(Guid eventId)
{
    while (await PublishAsync(outboxRepository => outboxRepository.ClaimAsync(eventId)))
    {
    }
}
```

## OutboxSettings

File `Messaging/Outbox/OutboxSettings.cs`, bound to the `Outbox` section; the defaults need no `appsettings.json` entry:

```csharp
public class OutboxSettings
```


```csharp
public const string Section = "Outbox";
```

```csharp
public const int TenantIdMaxLength = 64;
```

```csharp
public const int TraceParentMaxLength = 55;
```

```csharp
public int PollingIntervalMilliseconds { get; set; } = 1000;
```

```csharp
public int MaxBackoffSeconds { get; set; } = 60;
```

## OutboxModule

File `Messaging/Outbox/OutboxModule.cs`:

```csharp
public static class OutboxModule
```


```csharp
public static IServiceCollection AddOutboxModule(this IServiceCollection services, IConfiguration configuration)
{
    services.Configure<OutboxSettings>(configuration.GetSection(OutboxSettings.Section));
    services.AddScoped<OutboxRepository>();
    return services;
}
```

```csharp
public static IServiceCollection AddOutboxWorker(this IServiceCollection services)
{
    services.AddHostedService<OutboxWorker>();

    return services;
}
```

**after-command** registers the worker once, so the unit of work and the host share it:

```csharp
public static class OutboxModule
```

```csharp
public static IServiceCollection AddOutboxModule(this IServiceCollection services, IConfiguration configuration)
{
    services.Configure<OutboxSettings>(configuration.GetSection(OutboxSettings.Section));
    services.AddScoped<OutboxRepository>();
    services.AddSingleton<OutboxWorker>();
    return services;
}
```

```csharp
public static IServiceCollection AddOutboxWorker(this IServiceCollection services)
{
    services.AddHostedService(serviceProvider => serviceProvider.GetRequiredService<OutboxWorker>());

    return services;
}
```

## The kernel files that change

`InfrastructureModule` registers the outbox instead of `EventPublisher`:

```csharp
public static IServiceCollection AddInfrastructureModule(this IServiceCollection services, IConfiguration configuration)
{
    services.AddDbContext<AppDbContext>(options => options
        .UseNpgsql(configuration.GetConnectionString(PersistenceConstants.ConnectionStringName))
        .UseSnakeCaseNamingConvention());

    services.AddScoped<IUnitOfWork, UnitOfWork>();
    services.AddScoped<ICommandHandlerMiddleware, LoggingCommandHandlerMiddleware>();
    services.Configure<AuthSettings>(configuration.GetSection(AuthSettings.Section));
    services.AddOutboxModule(configuration);
    services.AddPersistenceModule();
    services.AddEventConsumerRegistry();

    return services;
}
```

`UnitOfWork` takes `OutboxRepository` instead of `EventPublisher` and `ILogger`, and writes the rows before `COMMIT`. **background**:

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

    outboxRepository.Add(queueEvents);

    await SaveChangesAsync();

    await _transaction.CommitAsync();
    await _transaction.DisposeAsync();

    _transaction = null;
    _transactionKey = null;

    var committedActions = _committedActions.ToList();

    _committedActions.Clear();

    foreach (var committedAction in committedActions)
    {
        await committedAction();
    }
}
```

**after-command** also takes `OutboxWorker` and publishes its own rows after `COMMIT`:

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

    outboxRepository.Add(queueEvents);

    await SaveChangesAsync();

    await _transaction.CommitAsync();
    await _transaction.DisposeAsync();

    _transaction = null;
    _transactionKey = null;

    foreach (var queueEvent in queueEvents)
    {
        await outboxWorker.PublishAsync(queueEvent.EventId);
    }

    var committedActions = _committedActions.ToList();

    _committedActions.Clear();

    foreach (var committedAction in committedActions)
    {
        await committedAction();
    }
}
```

## Hosts

The worker runs in exactly one kind of host: the Api when `hosts.worker` is `in-api`, the Worker when it is `separate`. Api `Program.cs`, after `.AddInfrastructureModule(builder.Configuration)`, only for `in-api`:

```csharp
    .AddOutboxWorker()
```

The Console never runs the worker. With `background` its commands only write rows; with `after-command` each command publishes its own rows before the Console exits.

### The Worker

Only when `hosts.worker` is `separate`. Create it with `dotnet new worker -n {Name}.Worker -o src/{Name}.Worker`, add it to the solution, remove the template's `Worker.cs`.

File `src/{Name}.Worker/{Name}.Worker.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Worker">
  <ItemGroup>
    <ProjectReference Include="../{Name}.Application/{Name}.Application.csproj" />
    <ProjectReference Include="../{Name}.Infrastructure/{Name}.Infrastructure.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Hosting" />
  </ItemGroup>
</Project>
```

File `src/{Name}.Worker/Program.cs`. `.AddOutboxWorker()` is there only with an outbox, and the `.AddEventConsumer<...>(...)` lines with `database` or `rabbitmq` (`event-consumers.md`):

```csharp
using {Name}.Infrastructure.Diagnostics;
using {Name}.Application.Orders;
using {Name}.Infrastructure.Messaging.Outbox;
using {Name}.Infrastructure.Scheduling;
using {Name}.Infrastructure.Messaging;
using {Name}.Application.Products;
using {Name}.Infrastructure.Logging;
using Microsoft.Extensions.Hosting;
using {Name}.Infrastructure;
using {Name}.Application;
using {Name}.Domain;
using {Name}.Shared;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddJsonLogging();

builder.Services
    .AddSharedModule()
    .AddDomainModule()
    .AddApplicationModule()
    .AddInfrastructureModule(builder.Configuration)
    .AddObservability(builder.Configuration)
    .AddScheduler(builder.Configuration)
    .AddRecurringJob<CancelEmptyOrdersJob>()
    .AddOutboxWorker()
    .AddEventConsumer<DecreaseStockWhenOrderLineAddedEventConsumer>(endpoint => endpoint.Retry(10))
    .AddEventConsumer<IncreaseStockWhenOrderLineCancelledEventConsumer>(endpoint => endpoint.Retry(10));

using var host = builder.Build();

await host.RunAsync();
```

File `src/{Name}.Worker/appsettings.json`; the `RabbitMq` connection string only with `events.transport` = `rabbitmq`:

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database={database};Username=postgres;Password=postgres",
    "RabbitMq": "amqp://guest:guest@localhost:5672/"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.Hosting": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  },
  "Observability": {
    "OtlpEndpoint": "",
    "OutboxLagThresholdSeconds": 60
  },
  "Caching": {
    "Seconds": 60
  },
  "Scheduler": {
    "Jobs": {
      "cancel-empty-orders": {
        "UserId": "",
        "UserEmail": "",
        "TenantId": ""
      }
    }
  }
}
```

## Migration

The table is part of the project's first migration (`infrastructure-postgres.md` "Migrations"), with or without the example.

## Tests

File `test/{Name}.Tests/Infrastructure/Messaging/Outbox/OutboxMessageTests.cs`. With `events.transport` = `rabbitmq` (one row per event, `Id` is the event's id, tenant kept):

```csharp
public class OutboxMessageTests
```

```csharp
[Fact]
public void From_Should_KeepTheEventIdAndTheTenantAndMakeTheRowDueAtOnce()
{
    var fakeQueueEvent = new FakeQueueEvent("first") { TenantId = "tenant-1" };

    var outboxMessage = OutboxMessage.From(fakeQueueEvent);

    Assert.Equal(fakeQueueEvent.EventId, outboxMessage.Id);
    Assert.Equal("tenant-1", outboxMessage.TenantId);
    Assert.Equal(fakeQueueEvent.OccurredAt, outboxMessage.NextAttemptAt);
    Assert.Equal(0, outboxMessage.Attempts);
}
```

```csharp
[Fact]
public void ToEvent_Should_ReadBackTheSameEventWithItsContext()
{
    var fakeQueueEvent = new FakeQueueEvent("first") { CorrelationId = Guid.CreateVersion7(), CausationId = Guid.CreateVersion7(), TenantId = "tenant-1", UserId = "user-1" };

    var readBack = OutboxMessage.From(fakeQueueEvent).ToEvent(typeof(FakeQueueEvent).Assembly);

    Assert.Equal(fakeQueueEvent, readBack);
}
```

```csharp
[Fact]
public void From_InsideAnActivity_Should_StoreThatActivitysIdAsTraceParent()
{
    using var recordedActivities = new RecordedActivities();
    using var activity = recordedActivities.TestSource.StartActivity("request");

    var outboxMessage = OutboxMessage.From(new FakeQueueEvent("first"));

    Assert.Equal(activity!.Id, outboxMessage.TraceParent);
}
```

```csharp
[Fact]
public void From_OutsideAnyActivity_Should_StoreNoTraceParent()
{
    Activity.Current = null;

    var outboxMessage = OutboxMessage.From(new FakeQueueEvent("first"));

    Assert.Null(outboxMessage.TraceParent);
}
```

With `database` (one row per event and consumer, a fresh `Id`, the consumer's attempt limit; using `{Name}.Infrastructure.Messaging` as well):

```csharp
public class OutboxMessageTests
```

```csharp
[Fact]
public void From_Should_MakeOneRowForTheEventAndTheConsumerDueAtOnceWithItsAttemptLimit()
{
    var fakeQueueEvent = new FakeQueueEvent("first");

    var outboxMessage = OutboxMessage.From(fakeQueueEvent, new EventConsumerEndpoint(typeof(RecordingEventConsumer)).Retry(4));

    Assert.Equal(fakeQueueEvent.EventId, outboxMessage.EventId);
    Assert.NotEqual(fakeQueueEvent.EventId, outboxMessage.Id);
    Assert.Equal(typeof(RecordingEventConsumer).FullName, outboxMessage.Consumer);
    Assert.Equal(5, outboxMessage.MaxAttempts);
    Assert.Equal(fakeQueueEvent.OccurredAt, outboxMessage.NextAttemptAt);
    Assert.Equal(0, outboxMessage.Attempts);
}
```

```csharp
[Fact]
public void ToEvent_Should_ReadBackTheSameEventWithItsContext()
{
    var fakeQueueEvent = new FakeQueueEvent("first") { CorrelationId = Guid.CreateVersion7(), CausationId = Guid.CreateVersion7(), TenantId = "tenant-1", UserId = "user-1" };

    var readBack = OutboxMessage.From(fakeQueueEvent, new EventConsumerEndpoint(typeof(RecordingEventConsumer))).ToEvent(typeof(FakeQueueEvent).Assembly);

    Assert.Equal(fakeQueueEvent, readBack);
}
```

The claim, the worker and the retry need a real database; the conformance suite proves them (`scripts/conformance/outbox.sh`).

## Decision record

`docs/adr/0011-outbox-and-publishing-models.md`, in `project-docs-adr.md`.

## Namespaces and usings

The `UnitOfWork` variants use `{Name}.Infrastructure.Messaging.Outbox` instead of `{Name}.Infrastructure.Messaging` and drop `Microsoft.Extensions.Logging`.

| File | Namespace | Usings |
|---|---|---|
| `src/{Name}.Infrastructure/Messaging/Outbox/OutboxMessage.cs` | `{Name}.Infrastructure.Messaging.Outbox` | System.Diagnostics, System.Reflection, System.Text.Json, {Name}.Shared.Domain |
| `src/{Name}.Infrastructure/Messaging/Outbox/OutboxMessageConfiguration.cs` | `{Name}.Infrastructure.Messaging.Outbox` | Microsoft.EntityFrameworkCore, Microsoft.EntityFrameworkCore.Metadata.Builders |
| `src/{Name}.Infrastructure/Messaging/Outbox/OutboxModule.cs` | `{Name}.Infrastructure.Messaging.Outbox` | Microsoft.Extensions.Configuration, Microsoft.Extensions.DependencyInjection |
| `src/{Name}.Infrastructure/Messaging/Outbox/OutboxRepository.cs` | `{Name}.Infrastructure.Messaging.Outbox` | Microsoft.EntityFrameworkCore, {Name}.Infrastructure.Persistence, {Name}.Shared.Domain |
| `src/{Name}.Infrastructure/Messaging/Outbox/OutboxSettings.cs` | `{Name}.Infrastructure.Messaging.Outbox` | none |
| `src/{Name}.Infrastructure/Messaging/Outbox/OutboxWorker.cs` | `{Name}.Infrastructure.Messaging.Outbox` | Microsoft.Extensions.DependencyInjection, Microsoft.Extensions.Hosting, Microsoft.Extensions.Logging, Microsoft.Extensions.Options, System.Diagnostics, {Name}.Contracts, {Name}.Infrastructure.Logging, {Name}.Infrastructure.Messaging.RabbitMq, {Name}.Shared.Diagnostics, {Name}.Shared.Persistence |
| `src/{Name}.Worker/Program.cs` | `` | Microsoft.Extensions.Hosting, {Name}.Application, {Name}.Application.Orders, {Name}.Application.Products, {Name}.Domain, {Name}.Infrastructure, {Name}.Infrastructure.Diagnostics, {Name}.Infrastructure.Logging, {Name}.Infrastructure.Messaging, {Name}.Infrastructure.Messaging.Outbox, {Name}.Infrastructure.Scheduling, {Name}.Shared |
| `test/{Name}.Tests/Infrastructure/Messaging/Outbox/OutboxMessageTests.cs` | `{Name}.Tests.Infrastructure.Messaging.Outbox` | System.Diagnostics, {Name}.Infrastructure.Messaging.Outbox, {Name}.Tests.TestSupport.Shared.Diagnostics, {Name}.Tests.TestSupport.Shared.Domain |
