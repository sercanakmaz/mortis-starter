# Event consumers, C#

Applies to every project. Project `{Name}.Infrastructure`, folder `Messaging/`; the lines that register consumers are in the host that runs them. Layer 1: `events.md`.

An event consumer is a class that reacts to a queue event after the command that raised it has committed: `{Action}When{Aggregate}{Event}EventConsumer : BaseEventConsumer<TEvent>` (`shared-domain.md`, `application.md`). Every consumer is a consumer of its own, in every publishing model: its own transaction, its own retries, its own failure.

## The one way to register a consumer

One line per consumer, in the `Program.cs` of the host that runs it, after `AddInfrastructureModule`:

```csharp
builder.Services
    .AddSharedModule()
    .AddDomainModule()
    .AddApplicationModule()
    .AddInfrastructureModule(builder.Configuration)
    .AddEventConsumer<DecreaseStockWhenOrderLineAddedEventConsumer>(endpoint => endpoint.Retry(10))
    .AddEventConsumer<IncreaseStockWhenOrderLineCancelledEventConsumer>(endpoint => endpoint.Retry(10));
```

- The file needs `using {Name}.Infrastructure.Messaging;` (the extension) and the `using` of each consumer's namespace (`{Name}.Application.Products`).
- Nothing is found by scanning: a consumer without its line never runs. `ApplicationModule` registers no consumer.
- Which host runs the consumers depends on the publishing model:

| `events.outbox` / `events.transport` | Hosts with the lines |
|---|---|
| `none` / `in-process` | `{Name}.Api` and `{Name}.Console` |
| `after-command` or `background` / `database` | every host that runs commands, `{Name}.Api` and `{Name}.Console` (they write one row per registered consumer), and `{Name}.Worker` when `hosts.worker` is `separate` (it runs the rows) |
| any outbox / `rabbitmq` | `{Name}.Worker` only |

- With `database`, a host writes outbox rows only for the consumers it registers, so every host in that row lists the same consumers.
- The endpoint's settings are code, never settings files: `Retry(n)` and, with `rabbitmq`, `Named(queue)`.
- `Retry(n)` means n retries after the first attempt, so `Retry(10)` is eleven attempts. It replaces every global retry setting:

| Model | What `Retry(n)` controls |
|---|---|
| `none` | `EventPublisher` runs the consumer up to n + 1 times, each in a fresh scope and transaction, then logs the failure |
| outbox with `database` | the row's `max_attempts` is n + 1; the row stays in the table when it reaches it |
| `rabbitmq` | the receive endpoint runs the consumer up to n + 1 times, then moves the message to `<queue>_error` |

## EventConsumerEndpoint

File `Messaging/EventConsumerEndpoint.cs`. One consumer's settings: the consumer, the event it takes (from its base class), the queue name and the retry count:

```csharp
public partial class EventConsumerEndpoint(Type consumerType)
```


```csharp
private const string ConsumerSuffix = "EventConsumer";
```

```csharp
public Type ConsumerType { get; } = consumerType;
```

```csharp
public Type EventType { get; } = EventTypeOf(consumerType);
```

```csharp
public string Queue { get; private set; } = QueueOf(consumerType);
```

```csharp
public int RetryCount { get; private set; } = 3;
```

```csharp
public EventConsumerEndpoint Retry(int retryCount)
{
    RetryCount = retryCount;

    return this;
}
```

```csharp
public EventConsumerEndpoint Named(string queue)
{
    Queue = queue;

    return this;
}
```

```csharp
public static string QueueOf(Type consumerType)
{
    var name = consumerType.Name.EndsWith(ConsumerSuffix) ? consumerType.Name[..^ConsumerSuffix.Length] : consumerType.Name;

    return WordStart().Replace(name, "-$1").ToLowerInvariant();
}
```

```csharp
public static Type EventTypeOf(Type consumerType)
{
    for (var type = consumerType; type is not null; type = type.BaseType)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(BaseEventConsumer<>)) return type.GetGenericArguments()[0];
    }

    throw new ArgumentException($"{consumerType.Name} is not a {nameof(BaseEventConsumer<>)}.");
}
```

```csharp
[GeneratedRegex("(?<!^)([A-Z])")]
private static partial Regex WordStart();
```

- The queue is the consumer's name in kebab case without `EventConsumer`: `DecreaseStockWhenOrderLineAddedEventConsumer` gets `decrease-stock-when-order-line-added`. Only `rabbitmq` uses it; the other models ignore it.
- Retry defaults to 3 when the action does not set it.

## EventConsumerModule

File `Messaging/EventConsumerModule.cs`. Generate the version of the chosen transport.

With `in-process` or `database`, the consumers go into an `EventConsumerRegistry` that `EventPublisher` or the outbox reads; `AddInfrastructureModule` calls `AddEventConsumerRegistry` so the registry exists even in a host with no consumer:

```csharp
public static class EventConsumerModule
```

```csharp
public static IServiceCollection AddEventConsumerRegistry(this IServiceCollection services)
{
    RegistryOf(services);

    return services;
}
```

```csharp
public static IServiceCollection AddEventConsumer<TConsumer>(this IServiceCollection services, Action<EventConsumerEndpoint> configure) where TConsumer : class, IEventConsumer
{
    var eventConsumerEndpoint = new EventConsumerEndpoint(typeof(TConsumer));

    configure(eventConsumerEndpoint);

    RegistryOf(services).Endpoints.Add(eventConsumerEndpoint);
    services.AddScoped<TConsumer>();

    return services;
}
```

```csharp
private static EventConsumerRegistry RegistryOf(IServiceCollection services)
{
    var eventConsumerRegistry = services.Select(descriptor => descriptor.ImplementationInstance).OfType<EventConsumerRegistry>().FirstOrDefault();

    if (eventConsumerRegistry is null)
    {
        eventConsumerRegistry = new EventConsumerRegistry();
        services.AddSingleton(eventConsumerRegistry);
    }

    return eventConsumerRegistry;
}
```

With `rabbitmq`, each line adds the consumer and its receive endpoint (`rabbitmq.md`); there is no registry:

```csharp
public static class EventConsumerModule
```

```csharp
public static IServiceCollection AddEventConsumer<TConsumer>(this IServiceCollection services, Action<EventConsumerEndpoint> configure) where TConsumer : class, IEventConsumer
{
    var eventConsumerEndpoint = new EventConsumerEndpoint(typeof(TConsumer));

    configure(eventConsumerEndpoint);

    services.AddScoped<TConsumer>();
    services.AddSingleton<IHostedService>(serviceProvider => ActivatorUtilities.CreateInstance<ReceiveEndpointService>(serviceProvider, eventConsumerEndpoint));

    return services;
}
```

## EventConsumerRegistry

File `Messaging/EventConsumerRegistry.cs`, only with `in-process` or `database`:

```csharp
public class EventConsumerRegistry
```


```csharp
public List<EventConsumerEndpoint> Endpoints { get; } = [];
```

```csharp
public IEnumerable<EventConsumerEndpoint> For(Type eventType) => Endpoints.Where(endpoint => endpoint.EventType == eventType);
```

```csharp
public EventConsumerEndpoint ByConsumer(string consumerName) => Endpoints.Single(endpoint => endpoint.ConsumerType.FullName == consumerName);
```

- `For(eventType)` returns the event's consumers in the order of their lines; `EventPublisher` runs them in that order and the outbox adds their rows in that order.
- `ByConsumer(name)` finds the endpoint of an outbox row's `consumer` column.

## Tests

File `test/{Name}.Tests/Infrastructure/Messaging/EventConsumerModuleTests.cs`, with `RecordingEventConsumer` and `OtherRecordingEventConsumer` in `TestSupport/Shared/Domain/` (`testing.md`). It uses the example's events, so it is written at step 10 and only when `example` is `true`. The whole class for `events.transport` = `in-process` or `database`:

```csharp
public class EventConsumerModuleTests
```

```csharp
[Fact]
public void AddEventConsumer_Should_AddOneEndpointPerConsumerAndRegisterTheConsumer()
{
    var services = new ServiceCollection();

    services.AddEventConsumerRegistry()
        .AddEventConsumer<RecordingEventConsumer>(endpoint => endpoint.Retry(1))
        .AddEventConsumer<OtherRecordingEventConsumer>(endpoint => endpoint.Retry(5));

    var eventConsumerRegistry = Assert.Single(services.Select(descriptor => descriptor.ImplementationInstance).OfType<EventConsumerRegistry>());
    Assert.Equal([1, 5], eventConsumerRegistry.For(typeof(FakeQueueEvent)).Select(endpoint => endpoint.RetryCount));
    Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(OtherRecordingEventConsumer) && descriptor.Lifetime == ServiceLifetime.Scoped);
}
```

```csharp
[Fact]
public void Endpoint_Should_TakeTheEventFromTheConsumerAndItsQueueFromTheConsumerName()
{
    var eventConsumerEndpoint = new EventConsumerEndpoint(typeof(DecreaseStockWhenOrderLineAddedEventConsumer));

    Assert.Equal(typeof(LineAdded), eventConsumerEndpoint.EventType);
    Assert.Equal("decrease-stock-when-order-line-added", eventConsumerEndpoint.Queue);
    Assert.Equal(3, eventConsumerEndpoint.RetryCount);
}
```

```csharp
[Fact]
public void Named_And_Retry_Should_OverrideTheQueueAndTheRetryCountInCode()
{
    var eventConsumerEndpoint = new EventConsumerEndpoint(typeof(DecreaseStockWhenOrderLineAddedEventConsumer)).Named("stock-decrease").Retry(5);

    Assert.Equal(["stock-decrease", "5"], [eventConsumerEndpoint.Queue, eventConsumerEndpoint.RetryCount.ToString()]);
}
```

**Only with `events.transport` = `rabbitmq`**, this test replaces the first test above (same position, different name); it counts the receive endpoints instead of reading a registry:

```csharp
[Fact]
public void AddEventConsumer_Should_RegisterTheConsumerAndOneReceiveEndpointPerConsumer()
{
    var services = new ServiceCollection();

    services.AddEventConsumer<RecordingEventConsumer>(endpoint => endpoint.Retry(1))
        .AddEventConsumer<OtherRecordingEventConsumer>(endpoint => endpoint.Retry(5));

    Assert.Equal(2, services.Count(descriptor => descriptor.ServiceType == typeof(IHostedService)));
    Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(OtherRecordingEventConsumer) && descriptor.Lifetime == ServiceLifetime.Scoped);
}
```

## Namespaces and usings

Every file of this page, with its namespace and the `using` lines it needs (`ImplicitUsings` covers `System`, `System.Linq`, `System.Threading.Tasks` and the like). A file the table does not list follows the namespace rule in `naming.md`.

| File | Namespace | Usings |
|---|---|---|
| `src/{Name}.Infrastructure/Messaging/EventConsumerEndpoint.cs` | `{Name}.Infrastructure.Messaging` | System.Text.RegularExpressions, {Name}.Shared.Domain |
| `src/{Name}.Infrastructure/Messaging/EventConsumerModule.cs` | `{Name}.Infrastructure.Messaging` | Microsoft.Extensions.DependencyInjection, {Name}.Shared.Domain |
| `src/{Name}.Infrastructure/Messaging/EventConsumerRegistry.cs` | `{Name}.Infrastructure.Messaging` | none |
| `test/{Name}.Tests/Infrastructure/Messaging/EventConsumerModuleTests.cs` | `{Name}.Tests.Infrastructure.Messaging` | Microsoft.Extensions.DependencyInjection, {Name}.Application.Products, {Name}.Domain.Aggregates.Orders.Events, {Name}.Infrastructure.Messaging, {Name}.Tests.TestSupport.Shared.Domain |
