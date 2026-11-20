# The outbox, C#

Applies when `events.outbox` is `background` (publish in the command) or `after-command` (publish after the command). Project `{Name}.Infrastructure`, folder `Messaging/Outbox/`; the Worker project when `hosts.worker` is `separate`. Layer 1: `events.md` "The three publishing models", `options/events.md`.

Generate only the chosen mode. The files below are the `background` code; a box marked **after-command** lists the only additions of that mode. With `none` nothing on this page exists (`events-inprocess.md`).

## How it fits together

```
command -> unitOfWork.CommitTransactionAsync
  save -> sync handlers -> collect the queue events
  outboxRepository.Add(events)          one row per event, in the SAME transaction
  save, COMMIT
  after-command only: outboxWorker.PublishAsync(id) for each of its rows

OutboxWorker (hosted, every PollingIntervalMilliseconds)
  repeat until nothing is due:
    new scope, BEGIN
    claim the next due row ("for update skip locked")
    run the event's async handlers in this transaction
    delete the row, COMMIT
    on failure: ROLLBACK, count the attempt, wait 2^attempts seconds
```

- One row, its handlers and its deletion are one transaction: a row is either fully handled and gone, or untouched and retried. No inbox is needed.
- `skip locked` lets two workers, or a request and a worker, run side by side; each row goes to one of them.
- A row that reaches `MaxAttempts` stays in the table for an operator. To retry it: `update outbox_messages set attempts = 0, next_attempt_at = now() where id = '<id>'`.
- Rows are taken in `occurred_at, id` order; a retried row can come after a later one.

## The files

| File | Job |
|---|---|
| `OutboxMessage.cs` | The row: the event as JSON, its attempts, when to try next, the last error |
| `OutboxMessageConfiguration.cs` | Its table, `outbox_messages` |
| `OutboxRepository.cs` | Add rows, claim the next due row, delete it, record a failure |
| `OutboxWorker.cs` | The background loop that publishes, sweeps and retries |
| `OutboxSettings.cs` | Polling interval and maximum attempts |
| `OutboxModule.cs` | The registrations |

## OutboxMessage

File `Messaging/Outbox/OutboxMessage.cs`:

```csharp
public class OutboxMessage
```

```csharp
public class OutboxMessage
```

```csharp
public Guid Id { get; private set; }
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
public DateTime NextAttemptAt { get; private set; }
```

```csharp
public string LastError { get; private set; }
```

```csharp
public static OutboxMessage From(EventBase @event)
{
    return new OutboxMessage
    {
        Id = @event.EventId,
        Type = @event.GetType().FullName,
        Payload = JsonSerializer.Serialize(@event, @event.GetType()),
        OccurredAt = @event.OccurredAt,
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

- The row's id is the event id. The correlation, causation, tenant and user travel inside the payload, as they do on the event.
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
    builder.Property(message => message.Type).HasMaxLength(300).IsRequired();
    builder.Property(message => message.Payload).HasColumnType("jsonb").IsRequired();

    builder.HasIndex(message => message.NextAttemptAt);

    builder.ToTable("outbox_messages");
}
```

## OutboxRepository

File `Messaging/Outbox/OutboxRepository.cs`, scoped, on the request's `AppDbContext`:

```csharp
public class OutboxRepository(AppDbContext context)
```

```csharp
public class OutboxRepository(AppDbContext context)
```

```csharp
public void Add(IEnumerable<EventBase> events)
{
    context.Set<OutboxMessage>().AddRange(events.Select(OutboxMessage.From));
}
```

```csharp
public async Task<OutboxMessage> ClaimNextAsync(int maxAttempts)
{
    var messages = await context.Set<OutboxMessage>()
        .FromSql($"select * from outbox_messages where attempts < {maxAttempts} and next_attempt_at <= {DateTime.UtcNow} order by occurred_at, id limit 1 for update skip locked")
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

**after-command** adds one member, to claim a row by its id:

```csharp
public async Task<OutboxMessage> ClaimAsync(Guid id)
{
    var messages = await context.Set<OutboxMessage>()
        .FromSql($"select * from outbox_messages where id = {id} for update skip locked")
        .ToListAsync();

    return messages.SingleOrDefault();
}
```

## OutboxWorker

File `Messaging/Outbox/OutboxWorker.cs`:

```csharp
public class OutboxWorker(IServiceScopeFactory serviceScopeFactory, IOptions<OutboxSettings> settings, ILogger<OutboxWorker> logger) : BackgroundService
```

```csharp
public class OutboxWorker(IServiceScopeFactory serviceScopeFactory, IOptions<OutboxSettings> settings, ILogger<OutboxWorker> logger) : BackgroundService
```

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        try
        {
            while (await PublishAsync(outboxRepository => outboxRepository.ClaimNextAsync(settings.Value.MaxAttempts)))
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
        var eventHandlers = scope.ServiceProvider
            .GetServices(typeof(BaseAsyncEventHandler<>).MakeGenericType(@event.GetType()))
            .Cast<IAsyncEventHandler>()
            .OrderBy(eventHandler => eventHandler.Sequence);

        foreach (var eventHandler in eventHandlers)
        {
            await eventHandler.Handle(@event);
        }

        outboxRepository.Remove(message);

        await unitOfWork.CommitTransactionAsync(nameof(OutboxWorker));

        logger.LogInformation("Published {Event} {EventId}", @event.GetType().Name, @event.EventId);
    }
    catch (Exception exception)
    {
        await unitOfWork.RollbackTransactionAsync();
        await outboxRepository.RecordFailureAsync(message, exception);

        logger.LogWarning(exception, "Async handler failed for {Event} {EventId}, attempt {Attempt}", @event.GetType().Name, @event.EventId, message.Attempts + 1);
    }

    return true;
}
```

- Its log lines carry the event's fields (`logging.md`): `CorrelationId`, `CausationId`, `TenantId`, `UserId`, `Event`, `EventId`.
- The handlers' commands join the worker's transaction; events they raise become rows in it.

**after-command** adds one member, which the unit of work calls right after `COMMIT`:

```csharp
public Task PublishAsync(Guid id)
{
    return PublishAsync(outboxRepository => outboxRepository.ClaimAsync(id));
}
```

## OutboxSettings

File `Messaging/Outbox/OutboxSettings.cs`, bound to the `Outbox` section; the defaults need no `appsettings.json` entry:

```csharp
public class OutboxSettings
```

```csharp
public class OutboxSettings
```

```csharp
public const string Section = "Outbox";
```

```csharp
public int PollingIntervalMilliseconds { get; set; } = 1000;
```

```csharp
public int MaxAttempts { get; set; } = 10;
```

## OutboxModule

File `Messaging/Outbox/OutboxModule.cs`:

```csharp
public static class OutboxModule
```

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
    services.AddOutboxModule(configuration);
    services.AddPersistenceModule();

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

File `src/{Name}.Worker/Program.cs`. `.AddOutboxWorker()` is there only with an outbox; a Worker for another add-on starts without it:

```csharp
using {Name}.Infrastructure.Messaging.Outbox;
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
    .AddOutboxWorker();

using var host = builder.Build();

await host.RunAsync();
```

File `src/{Name}.Worker/appsettings.json`:

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

## Migration

The table is part of the project's first migration (`infrastructure-postgres.md` "Migrations"), with or without the example.

## Tests

File `test/{Name}.Tests/Infrastructure/Messaging/Outbox/OutboxMessageTests.cs`:

```csharp
public class OutboxMessageTests
```

```csharp
[Fact]
public void From_Should_KeepTheEventIdAndMakeTheRowDueAtOnce()
{
    var fakeQueueEvent = new FakeQueueEvent("first");

    var outboxMessage = OutboxMessage.From(fakeQueueEvent);

    Assert.Equal(fakeQueueEvent.EventId, outboxMessage.Id);
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

The claim, the worker and the retry need a real database; the conformance suite proves them (`scripts/conformance/outbox.sh`).

## Decision record

`docs/adr/0011-outbox-and-publishing-models.md`, in `project-docs-adr.md`.

## Namespaces and usings

The `UnitOfWork` variants use `{Name}.Infrastructure.Messaging.Outbox` instead of `{Name}.Infrastructure.Messaging` and drop `Microsoft.Extensions.Logging`.

| File | Namespace | Usings |
|---|---|---|
| `src/{Name}.Infrastructure/Messaging/Outbox/OutboxMessage.cs` | `{Name}.Infrastructure.Messaging.Outbox` | System.Reflection, System.Text.Json, {Name}.Shared.Domain |
| `src/{Name}.Infrastructure/Messaging/Outbox/OutboxMessageConfiguration.cs` | `{Name}.Infrastructure.Messaging.Outbox` | Microsoft.EntityFrameworkCore, Microsoft.EntityFrameworkCore.Metadata.Builders |
| `src/{Name}.Infrastructure/Messaging/Outbox/OutboxModule.cs` | `{Name}.Infrastructure.Messaging.Outbox` | Microsoft.Extensions.Configuration, Microsoft.Extensions.DependencyInjection |
| `src/{Name}.Infrastructure/Messaging/Outbox/OutboxRepository.cs` | `{Name}.Infrastructure.Messaging.Outbox` | Microsoft.EntityFrameworkCore, {Name}.Infrastructure.Persistence, {Name}.Shared.Domain |
| `src/{Name}.Infrastructure/Messaging/Outbox/OutboxSettings.cs` | `{Name}.Infrastructure.Messaging.Outbox` | none |
| `src/{Name}.Infrastructure/Messaging/Outbox/OutboxWorker.cs` | `{Name}.Infrastructure.Messaging.Outbox` | Microsoft.Extensions.DependencyInjection, Microsoft.Extensions.Hosting, Microsoft.Extensions.Logging, Microsoft.Extensions.Options, {Name}.Domain, {Name}.Infrastructure.Logging, {Name}.Shared.Domain, {Name}.Shared.Persistence |
| `src/{Name}.Worker/Program.cs` | `` | Microsoft.Extensions.Hosting, {Name}.Application, {Name}.Domain, {Name}.Infrastructure, {Name}.Infrastructure.Logging, {Name}.Infrastructure.Messaging.Outbox, {Name}.Shared |
| `test/{Name}.Tests/Infrastructure/Messaging/Outbox/OutboxMessageTests.cs` | `{Name}.Tests.Infrastructure.Messaging.Outbox` | {Name}.Infrastructure.Messaging.Outbox, {Name}.Tests.TestSupport.Shared.Domain |
