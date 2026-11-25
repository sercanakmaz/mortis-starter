# The RabbitMQ bus, C#

Applies when `events.transport` is `rabbitmq` (it needs an outbox and, by rule R3, a worker host). Project `{Name}.Infrastructure`, folder `Messaging/RabbitMq/`; the consumers in the Worker. Layer 1: `events.md` "Worker", `options/events.md`. The wire format is MassTransit 8's, checked against MassTransit 8.5 with both its serializers.

Our own small bus on `RabbitMQ.Client`: it publishes outbox rows and consumes them in the Worker. Every event consumer is a consumer of its own, as in MassTransit: its own queue, transaction, retries and error queue. A MassTransit 8 service consumes what we publish, and we read what it publishes. No MassTransit package is referenced.

## How it fits together

```
OutboxWorker (Api or Worker)       claim a row, RabbitMqPublisher.PublishAsync, wait for the broker's confirm, delete the row, COMMIT
RabbitMqPublisher                  envelope (MassTransit 8 JSON) to the durable fanout exchange "<Namespace>:<TypeName>"
ReceiveEndpointService (Worker)    one per AddEventConsumer line: queue Q (the consumer's name), exchange Q, the event's exchange bound to Q, Q_error
  per message                      read the envelope; not the consumer's event: acknowledge; else new scope, BEGIN, the consumer, COMMIT
  on failure                       retry Retry(n) times, then publish the body to exchange Q_error with the MT-Fault headers, ack
```

- One consumer's failure never touches another consumer's queue: each retries and fails alone.
- A message whose types do not include the consumer's event is acknowledged and logged.
- The broker delivers at least once: a message can reach a consumer twice (a crash after its commit and before the acknowledgement, a confirm lost on the way back). **Every event consumer must be safe to run twice**; the bus does not deduplicate. A consumer that cannot be made so naturally keeps its own record of what it handled, in its own tables, in the same transaction.
- A broker that is down fails the publish, so the row stays and the outbox retries it with its backoff. A receive endpoint that cannot reach the broker tries again every `ReconnectSeconds`; the connection recovers by itself after that.
- An endpoint's configuration is code, one `AddEventConsumer<TConsumer>(endpoint => ...)` line per consumer in the Worker (`event-consumers.md`), never settings: `Retry(n)` and, when the default name does not fit, `Named(queue)`.

## The files

| File | Job |
|---|---|
| `RabbitMqConstants.cs` | Content type, address prefix, error suffix, queue type, prefetch, reconnect delay |
| `MessageNames.cs` | Exchange name and message URNs of a type |
| `MessageEnvelope.cs` | The MassTransit 8 envelope: build from an event, write, read, turn into an event |
| `RabbitMqConnection.cs` | One connection, a channel per use |
| `RabbitMqPublisher.cs` | Publish one event, with the broker's confirm |
| `ReceiveEndpointService.cs` | The consume loop of one consumer's endpoint; the consumer runs in one transaction |
| `RabbitMqModule.cs` | The connection and the publisher |

## Names

File `Messaging/RabbitMq/RabbitMqConstants.cs`:

```csharp
public static class RabbitMqConstants
```


```csharp
public const string ConnectionStringName = "RabbitMq";
```

```csharp
public const string ContentType = "application/vnd.masstransit+json";
```

```csharp
public const string UrnPrefix = "urn:message:";
```

```csharp
public const string AddressPrefix = "rabbitmq://localhost/";
```

```csharp
public const string ErrorSuffix = "_error";
```

```csharp
public const string QueueTypeArgument = "x-queue-type";
```

```csharp
public const string ClassicQueueType = "classic";
```

```csharp
public const ushort PrefetchCount = 16;
```

```csharp
public const int ReconnectSeconds = 5;
```

```csharp
public const string TraceParentHeader = "traceparent";
```

File `Messaging/RabbitMq/MessageNames.cs`:

```csharp
public static class MessageNames
```


```csharp
public static string Exchange(Type messageType)
{
    if (messageType.GetCustomAttribute<MessageNameAttribute>() is { } messageName)
    {
        return messageName.Name;
    }

    var name = messageType.IsNested ? $"{messageType.DeclaringType!.Name}+{messageType.Name}" : messageType.Name;

    return $"{messageType.Namespace}:{name}";
}
```

```csharp
public static string Urn(Type messageType) => RabbitMqConstants.UrnPrefix + Exchange(messageType);
```

```csharp
public static List<string> MessageTypes(Type messageType)
{
    var types = new List<Type>();

    for (var type = messageType; type is not null && type != typeof(object); type = type.BaseType)
    {
        types.Add(type);
    }

    types.AddRange(messageType.GetInterfaces().Where(contract => contract.Namespace?.StartsWith(nameof(System)) != true));

    return types.Select(Urn).ToList();
}
```

- `MessageTypes` lists the type, its base types and its interfaces outside `System`, as MassTransit does: `LineAdded` gives the URNs of `LineAdded`, `EventBase` and `IQueueEvent`.
- Renaming or moving an event type changes its exchange; a consumer elsewhere stops hearing it.

## The envelope

File `Messaging/RabbitMq/MessageEnvelope.cs`:

```csharp
public class MessageEnvelope
```


```csharp
public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = JsonIgnoreCondition.Never };
```

```csharp
public Guid? MessageId { get; set; }
```

```csharp
public Guid? RequestId { get; set; }
```

```csharp
public Guid? CorrelationId { get; set; }
```

```csharp
public Guid? ConversationId { get; set; }
```

```csharp
public Guid? InitiatorId { get; set; }
```

```csharp
public string SourceAddress { get; set; }
```

```csharp
public string DestinationAddress { get; set; }
```

```csharp
public string ResponseAddress { get; set; }
```

```csharp
public string FaultAddress { get; set; }
```

```csharp
public List<string> MessageType { get; set; } = [];
```

```csharp
public JsonElement Message { get; set; }
```

```csharp
public DateTime? ExpirationTime { get; set; }
```

```csharp
public DateTime? SentTime { get; set; }
```

```csharp
public Dictionary<string, object> Headers { get; set; } = [];
```

```csharp
public Dictionary<string, object> Host { get; set; } = [];
```

```csharp
public static MessageEnvelope From(EventBase @event, string sourceAddress)
{
    var eventType = @event.GetType();
    var process = Process.GetCurrentProcess();

    return new MessageEnvelope
    {
        MessageId = @event.EventId,
        CorrelationId = @event.CorrelationId,
        ConversationId = @event.CorrelationId,
        InitiatorId = @event.CausationId,
        SourceAddress = RabbitMqConstants.AddressPrefix + sourceAddress,
        DestinationAddress = RabbitMqConstants.AddressPrefix + MessageNames.Exchange(eventType),
        MessageType = MessageNames.MessageTypes(eventType),
        Message = JsonSerializer.SerializeToElement(@event, eventType, Options),
        SentTime = DateTime.UtcNow,
        Headers = new Dictionary<string, object>
        {
            ["Mortis-Tenant-Id"] = @event.TenantId,
            ["Mortis-User-Id"] = @event.UserId,
            ["Mortis-User-Email"] = @event.UserEmail,
            ["Mortis-Language"] = @event.Language,
            [RabbitMqConstants.TraceParentHeader] = Activity.Current?.Id
        },
        Host = new Dictionary<string, object>
        {
            ["machineName"] = Environment.MachineName,
            ["processName"] = process.ProcessName,
            ["processId"] = Environment.ProcessId,
            ["assembly"] = eventType.Assembly.GetName().Name,
            ["assemblyVersion"] = eventType.Assembly.GetName().Version?.ToString(),
            ["frameworkVersion"] = Environment.Version.ToString(),
            ["operatingSystemVersion"] = Environment.OSVersion.ToString()
        }
    };
}
```

```csharp
public static MessageEnvelope Read(ReadOnlySpan<byte> body) => JsonSerializer.Deserialize<MessageEnvelope>(body, Options);
```

```csharp
public byte[] ToBytes() => JsonSerializer.SerializeToUtf8Bytes(this, Options);
```

```csharp
public EventBase ToEvent(Type eventType)
{
    var @event = (EventBase)Message.Deserialize(eventType, Options);

    return @event.CorrelationId == Guid.Empty && CorrelationId is { } correlationId
        ? @event with { CorrelationId = correlationId }
        : @event;
}
```

- `messageId` is the event id, so a consumer that deduplicates can use it; `correlationId` and `conversationId` are the event's correlation; `initiatorId` is its causation.
- The context also travels as `Mortis-Tenant-Id`, `Mortis-User-Id`, `Mortis-User-Email` and `Mortis-Language` headers, for consumers that are not ours.
- `Read` matches names without regard to case and ignores unknown members, so a Newtonsoft envelope (PascalCase, nulls left out) reads too. A message without our context fields takes the envelope's correlation.

## Publishing

File `Messaging/RabbitMq/RabbitMqConnection.cs`, a singleton:

```csharp
public sealed class RabbitMqConnection(IConfiguration configuration) : IAsyncDisposable
```


```csharp
private readonly SemaphoreSlim _lock = new(1, 1);
```

```csharp
private IConnection _connection;
```

```csharp
public async Task<IChannel> CreateChannelAsync(bool publisherConfirms, CancellationToken cancellationToken)
{
    await _lock.WaitAsync(cancellationToken);

    try
    {
        if (_connection is not { IsOpen: true })
        {
            var connectionFactory = new ConnectionFactory
            {
                Uri = new Uri(configuration.GetConnectionString(RabbitMqConstants.ConnectionStringName)),
                AutomaticRecoveryEnabled = true
            };

            _connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        }
    }
    finally
    {
        _lock.Release();
    }

    return await _connection.CreateChannelAsync(new CreateChannelOptions(publisherConfirms, publisherConfirms), cancellationToken);
}
```

```csharp
public async ValueTask DisposeAsync()
{
    if (_connection is not null)
    {
        await _connection.DisposeAsync();
    }
}
```

File `Messaging/RabbitMq/RabbitMqPublisher.cs`:

```csharp
public class RabbitMqPublisher(RabbitMqConnection rabbitMqConnection)
```


```csharp
private static readonly string Source = AppDomain.CurrentDomain.FriendlyName.ToLowerInvariant();
```

```csharp
public async Task PublishAsync(EventBase @event, CancellationToken cancellationToken)
{
    var exchange = MessageNames.Exchange(@event.GetType());
    var envelope = MessageEnvelope.From(@event, Source);

    await using var channel = await rabbitMqConnection.CreateChannelAsync(true, cancellationToken);

    await channel.ExchangeDeclareAsync(exchange, ExchangeType.Fanout, durable: true, autoDelete: false, cancellationToken: cancellationToken);

    var basicProperties = new BasicProperties
    {
        ContentType = RabbitMqConstants.ContentType,
        DeliveryMode = DeliveryModes.Persistent,
        MessageId = envelope.MessageId.ToString(),
        CorrelationId = envelope.CorrelationId.ToString()
    };

    await channel.BasicPublishAsync(exchange, string.Empty, false, basicProperties, envelope.ToBytes(), cancellationToken);
}
```

- The exchange is declared on every publish with the arguments MassTransit uses (durable fanout, no arguments), so neither side gets `PRECONDITION_FAILED`.
- With publisher confirms on, `BasicPublishAsync` returns only after the broker took the message, and throws when it refused it.

`OutboxWorker` takes `RabbitMqPublisher` and publishes the row instead of running a consumer; there is one row per event, with no `EventId`, `Consumer` or `MaxAttempts` column (`OutboxMessage.From(event)`, `OutboxRepository.Add` adds one row per event). Publishing is retried until the broker takes it, waiting 2^attempts seconds but never more than `OutboxSettings.MaxBackoffSeconds`; everything else is as on `outbox.md`:

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

    var @event = message.ToEvent(typeof(ContractsMarker).Assembly);

    ActivityContext.TryParse(message.TraceParent, null, out var parent);
    using var activity = Telemetry.Source.StartActivity($"publish {@event.GetType().Name}", ActivityKind.Producer, parent);

    using var logScope = logger.BeginScope(LogFields.Of(@event));

    try
    {
        await rabbitMqPublisher.PublishAsync(@event, CancellationToken.None);

        outboxRepository.Remove(message);

        await unitOfWork.CommitTransactionAsync(nameof(OutboxWorker));

        logger.LogInformation("Published {Event} {EventId}", @event.GetType().Name, @event.EventId);
    }
    catch (Exception exception)
    {
        await unitOfWork.RollbackTransactionAsync();
        await outboxRepository.RecordFailureAsync(message, exception, settings.Value.MaxBackoffSeconds);

        logger.LogWarning(exception, "Publishing {Event} {EventId} to RabbitMQ failed, attempt {Attempt}", @event.GetType().Name, @event.EventId, message.Attempts + 1);
    }

    return true;
}
```

## Consuming

The consumer's endpoint is an `EventConsumerEndpoint` (`event-consumers.md`); its `Queue` is the queue name. File `Messaging/RabbitMq/ReceiveEndpointService.cs`, one per `AddEventConsumer` line:

```csharp
public class ReceiveEndpointService(EventConsumerEndpoint eventConsumerEndpoint, RabbitMqConnection rabbitMqConnection, IServiceScopeFactory serviceScopeFactory, ILogger<ReceiveEndpointService> logger) : BackgroundService
```


```csharp
private readonly string _errorQueue = eventConsumerEndpoint.Queue + RabbitMqConstants.ErrorSuffix;
```

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        try
        {
            await ConsumeAsync(stoppingToken);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            logger.LogError(exception, "Receive endpoint {Queue} cannot reach RabbitMQ; it tries again in {Seconds} seconds", eventConsumerEndpoint.Queue, RabbitMqConstants.ReconnectSeconds);

            await Task.Delay(TimeSpan.FromSeconds(RabbitMqConstants.ReconnectSeconds), stoppingToken);
        }
    }
}
```

```csharp
private async Task ConsumeAsync(CancellationToken stoppingToken)
{
    await using var channel = await rabbitMqConnection.CreateChannelAsync(false, stoppingToken);
    var queueArguments = new Dictionary<string, object> { [RabbitMqConstants.QueueTypeArgument] = RabbitMqConstants.ClassicQueueType };

    foreach (var queue in new[] { eventConsumerEndpoint.Queue, _errorQueue })
    {
        await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false, arguments: queueArguments, cancellationToken: stoppingToken);
        await channel.ExchangeDeclareAsync(queue, ExchangeType.Fanout, durable: true, autoDelete: false, cancellationToken: stoppingToken);
        await channel.QueueBindAsync(queue, queue, string.Empty, cancellationToken: stoppingToken);
    }

    var exchange = MessageNames.Exchange(eventConsumerEndpoint.EventType);
    var urn = MessageNames.Urn(eventConsumerEndpoint.EventType);

    await channel.ExchangeDeclareAsync(exchange, ExchangeType.Fanout, durable: true, autoDelete: false, cancellationToken: stoppingToken);
    await channel.ExchangeBindAsync(eventConsumerEndpoint.Queue, exchange, string.Empty, cancellationToken: stoppingToken);

    var consumer = new AsyncEventingBasicConsumer(channel);

    consumer.ReceivedAsync += async (_, delivery) =>
    {
        var envelope = MessageEnvelope.Read(delivery.Body.Span);
        var eventType = eventConsumerEndpoint.EventType;

        if (!envelope.MessageType.Contains(urn))
        {
            logger.LogWarning("No consumer on {Queue} for {MessageType}; the message is acknowledged", eventConsumerEndpoint.Queue, string.Join(", ", envelope.MessageType));
            await channel.BasicAckAsync(delivery.DeliveryTag, false, stoppingToken);
            return;
        }

        var @event = envelope.ToEvent(eventType);
        ActivityContext.TryParse(envelope.Headers.GetValueOrDefault(RabbitMqConstants.TraceParentHeader)?.ToString(), null, out var parent);
        using var activity = Telemetry.Source.StartActivity($"consume {eventType.Name}", ActivityKind.Consumer, parent);
        using var logScope = logger.BeginScope(LogFields.Of(@event));

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await ConsumeAsync(@event);

                logger.LogInformation("Handled {Event} {EventId} by {EventConsumer}", eventType.Name, @event.EventId, eventConsumerEndpoint.ConsumerType.FullName);
                break;
            }
            catch (Exception exception) when (attempt <= eventConsumerEndpoint.RetryCount)
            {
                Telemetry.ConsumerFailures.Add(1, new KeyValuePair<string, object>("consumer", eventConsumerEndpoint.ConsumerType.Name));
                logger.LogWarning(exception, "Event consumer {EventConsumer} failed for {Event} {EventId}, attempt {Attempt}", eventConsumerEndpoint.ConsumerType.FullName, eventType.Name, @event.EventId, attempt);
            }
            catch (Exception exception)
            {
                Telemetry.ConsumerFailures.Add(1, new KeyValuePair<string, object>("consumer", eventConsumerEndpoint.ConsumerType.Name));
                logger.LogError(exception, "Event consumer {EventConsumer} failed for {Event} {EventId}; moved to {ErrorQueue}", eventConsumerEndpoint.ConsumerType.FullName, eventType.Name, @event.EventId, _errorQueue);
```

The member continues:

```csharp
                var faultProperties = new BasicProperties(delivery.BasicProperties)
                {
                    Headers = new Dictionary<string, object>(delivery.BasicProperties.Headers ?? new Dictionary<string, object>())
                    {
                        ["MT-Reason"] = "fault",
                        ["MT-Fault-ExceptionType"] = exception.GetType().FullName,
                        ["MT-Fault-Message"] = exception.Message,
                        ["MT-Fault-StackTrace"] = exception.StackTrace,
                        ["MT-Fault-Timestamp"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                        ["MT-Fault-ConsumerType"] = eventConsumerEndpoint.ConsumerType.FullName,
                        ["MT-Fault-InputAddress"] = RabbitMqConstants.AddressPrefix + eventConsumerEndpoint.Queue,
                        ["MT-Fault-MessageType"] = eventType.FullName,
                        ["MT-Host-MachineName"] = Environment.MachineName
                    }
                };

                await channel.BasicPublishAsync(_errorQueue, string.Empty, false, faultProperties, delivery.Body, stoppingToken);
                break;
            }
        }

        await channel.BasicAckAsync(delivery.DeliveryTag, false, stoppingToken);
    };

    await channel.BasicQosAsync(0, RabbitMqConstants.PrefetchCount, false, stoppingToken);
    await channel.BasicConsumeAsync(eventConsumerEndpoint.Queue, autoAck: false, consumer, stoppingToken);

    while (channel.IsOpen)
    {
        await Task.Delay(TimeSpan.FromSeconds(RabbitMqConstants.ReconnectSeconds), stoppingToken);
    }
}
```

```csharp
private async Task ConsumeAsync(EventBase @event)
{
    using var scope = serviceScopeFactory.CreateScope();
    scope.ServiceProvider.GetRequiredService<TenantScope>().Enter(@event.TenantId);
    var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
    var eventConsumer = (IEventConsumer)scope.ServiceProvider.GetRequiredService(eventConsumerEndpoint.ConsumerType);

    try
    {
        await unitOfWork.BeginTransactionAsync(nameof(ReceiveEndpointService));
        await eventConsumer.Handle(@event);
        await unitOfWork.CommitTransactionAsync(nameof(ReceiveEndpointService));
    }
    catch (Exception)
    {
        await unitOfWork.RollbackTransactionAsync();

        throw;
    }
}
```

- Queues are declared `classic` and durable, exactly as MassTransit 8 declares them; the same queue name can be declared by either side.
- The fault headers are MassTransit's: `MT-Reason` = `fault`, `MT-Fault-ExceptionType`, `MT-Fault-Message`, `MT-Fault-StackTrace`, `MT-Fault-Timestamp`, `MT-Fault-ConsumerType`, `MT-Fault-InputAddress`, `MT-Fault-MessageType`.
- The consumers restore the context from the event (`ContextOf(@event)`); their commands join the consumer's transaction, and the events they raise become outbox rows in it.

## Registration

File `Messaging/RabbitMq/RabbitMqModule.cs`:

```csharp
public static class RabbitMqModule
```


```csharp
public static IServiceCollection AddRabbitMqModule(this IServiceCollection services)
{
    services.AddSingleton<RabbitMqConnection>();
    services.AddSingleton<RabbitMqPublisher>();

    return services;
}
```

`InfrastructureModule` adds one line after `services.AddOutboxModule(configuration);`:

```csharp
        services.AddRabbitMqModule();
```

The Worker's `Program.cs` adds one line per consumer after `.AddOutboxWorker()` (the whole file is on `outbox.md`, the `AddEventConsumer` of this transport on `event-consumers.md`):

```csharp
    .AddEventConsumer<DecreaseStockWhenOrderLineAddedEventConsumer>(endpoint => endpoint.Retry(10))
    .AddEventConsumer<IncreaseStockWhenOrderLineCancelledEventConsumer>(endpoint => endpoint.Retry(10));
```

The Api and the Console register no consumer.

Every host's `appsettings.json` gets `"RabbitMq": "amqp://guest:guest@localhost:5672/"` under `ConnectionStrings`; `docker-compose.yml` gets a `rabbitmq:4-management` service with a health check, and the Api depends on it.

Packages: `RabbitMQ.Client` 7.2.2 in `Directory.Packages.props` and `{Name}.Infrastructure.csproj`.

## Tests

File `test/{Name}.Tests/Infrastructure/Messaging/RabbitMq/MessageNamesTests.cs`:

```csharp
public class MessageNamesTests
```

```csharp
[Fact]
public void Exchange_Should_BeTheNamespaceAndTheTypeNameJoinedByAColon()
{
    Assert.Equal("{Name}.Contracts.Orders:LineAdded", MessageNames.Exchange(typeof(LineAdded)));
}
```

```csharp
[Fact]
public void MessageTypes_Should_ListTheTypeItsBaseTypesAndItsOwnInterfacesAsUrnsLikeMassTransit()
{
    Assert.Equal(
        ["urn:message:{Name}.Contracts.Orders:LineAdded", "urn:message:{Name}.Shared.Domain:EventBase", "urn:message:{Name}.Shared.Domain:IQueueEvent"],
        MessageNames.MessageTypes(typeof(LineAdded)));
}
```

```csharp
[Fact]
public void Exchange_WithAMessageName_Should_UseThePinnedName()
{
    Assert.Equal("Old.Namespace:Pinned", MessageNames.Exchange(typeof(PinnedProbe)));
}
```

File `test/{Name}.Tests/Infrastructure/Messaging/RabbitMq/MessageEnvelopeTests.cs`. The two bodies were written by MassTransit 8.5.12, one per serializer:

```csharp
public class MessageEnvelopeTests
```

```csharp
[Fact]
public void From_Should_UseTheEventIdAsMessageIdAndTheCorrelationAsCorrelationAndConversation()
{
    var lineAdded = new LineAdded("order-1", "line-1", "SKU-1", 3) { CorrelationId = Guid.CreateVersion7(), CausationId = Guid.CreateVersion7(), TenantId = "tenant-1" };

    var envelope = MessageEnvelope.From(lineAdded, "{database}-api");

    Assert.Equal(lineAdded.EventId, envelope.MessageId);
    Assert.Equal([lineAdded.CorrelationId, lineAdded.CorrelationId, lineAdded.CausationId], [envelope.CorrelationId, envelope.ConversationId, envelope.InitiatorId]);
    Assert.Equal("rabbitmq://localhost/{Name}.Contracts.Orders:LineAdded", envelope.DestinationAddress);
    Assert.Equal("tenant-1", envelope.Headers["Mortis-Tenant-Id"]);
}
```

```csharp
[Fact]
public void ToBytes_ThenRead_Should_GiveBackTheSameEvent()
{
    var lineAdded = new LineAdded("order-1", "line-1", "SKU-1", 3) { CorrelationId = Guid.CreateVersion7() };

    var readBack = MessageEnvelope.Read(MessageEnvelope.From(lineAdded, "{database}-api").ToBytes()).ToEvent(typeof(LineAdded));

    Assert.Equal(lineAdded, readBack);
}
```

```csharp
[Fact]
public void Read_WithTheBodyMassTransitWritesWithSystemTextJson_Should_GiveTheEventAndItsIds()
{
    const string body = """
        {"messageId":"19040000-e39c-f26b-89d6-08df47526602","requestId":null,"correlationId":"ca09b075-0317-4992-9052-79b9d6c89880","conversationId":"19040000-e39c-f26b-81dc-08df47526606","initiatorId":null,
         "sourceAddress":"rabbitmq://localhost/probe","destinationAddress":"rabbitmq://localhost/{Name}.Contracts.Orders:LineAdded","responseAddress":null,"faultAddress":null,
         "messageType":["urn:message:{Name}.Contracts.Orders:LineAdded","urn:message:{Name}.Shared.Domain:EventBase","urn:message:{Name}.Shared.Domain:IQueueEvent"],
         "message":{"id":"order-1","lineId":"line-1","sku":"SKU-1","quantity":3,"eventId":"7afaf008-ae39-4cf6-954c-2fefe5e608c1","occurredAt":"2026-11-20T22:05:33.3998Z","correlationId":"ca09b075-0317-4992-9052-79b9d6c89880","causationId":null,"tenantId":null,"userId":null},
         "expirationTime":null,"sentTime":"2026-11-20T22:05:33.4232534Z","headers":{},"host":{"machineName":"vm","processName":"probe","processId":1049,"massTransitVersion":"8.5.12.0"}}
        """;

    var envelope = MessageEnvelope.Read(Encoding.UTF8.GetBytes(body));
    var lineAdded = (LineAdded)envelope.ToEvent(typeof(LineAdded));

    Assert.Equal(Guid.Parse("19040000-e39c-f26b-89d6-08df47526602"), envelope.MessageId);
    Assert.Equal(["SKU-1", "3"], [lineAdded.Sku, lineAdded.Quantity.ToString()]);
    Assert.Equal(Guid.Parse("ca09b075-0317-4992-9052-79b9d6c89880"), lineAdded.CorrelationId);
}
```

```csharp
[Fact]
public void Read_WithTheBodyMassTransitWritesWithNewtonsoftAndNoContextFields_Should_TakeTheCorrelationFromTheEnvelope()
{
    const string body = """
        {"MessageId":"3d040000-e39c-f26b-8410-08df475268b1","CorrelationId":"18ec1e17-9271-40c4-9477-07ea8ca9a2d6","ConversationId":"3d040000-e39c-f26b-1bba-08df475268b5",
         "MessageType":["urn:message:{Name}.Contracts.Orders:LineAdded"],
         "Message":{"Id":"order-1","LineId":"line-1","Sku":"SKU-1","Quantity":3},"SentTime":"2026-11-20T22:05:37.9254288Z","Headers":{}}
        """;

    var lineAdded = (LineAdded)MessageEnvelope.Read(Encoding.UTF8.GetBytes(body)).ToEvent(typeof(LineAdded));

    Assert.Equal("SKU-1", lineAdded.Sku);
    Assert.Equal(Guid.Parse("18ec1e17-9271-40c4-9477-07ea8ca9a2d6"), lineAdded.CorrelationId);
}
```

```csharp
[Fact]
public void From_InsideAnActivity_Should_CarryItsIdInTheTraceParentHeader()
{
    using var recordedActivities = new RecordedActivities();
    using var activity = recordedActivities.TestSource.StartActivity("publish");

    var envelope = MessageEnvelope.From(new LineAdded("order-1", "line-1", "SKU-1", 3), "{database}-worker");

    Assert.Equal(activity!.Id, envelope.Headers[RabbitMqConstants.TraceParentHeader]);
}
```

The broker behaviour is proven by the conformance suite (`scripts/conformance/transport-rabbitmq.sh`).

## Decision record

`docs/adr/0013-own-bus-compatible-with-masstransit.md`, in `project-docs-adr.md`.

## Namespaces and usings

| File | Namespace | Usings |
|---|---|---|
| `src/{Name}.Infrastructure/Messaging/RabbitMq/MessageEnvelope.cs` | `{Name}.Infrastructure.Messaging.RabbitMq` | System.Diagnostics, System.Text.Json, System.Text.Json.Serialization, {Name}.Shared.Domain |
| `src/{Name}.Infrastructure/Messaging/RabbitMq/MessageNames.cs` | `{Name}.Infrastructure.Messaging.RabbitMq` | System.Reflection, {Name}.Shared.Domain |
| `src/{Name}.Infrastructure/Messaging/RabbitMq/RabbitMqConnection.cs` | `{Name}.Infrastructure.Messaging.RabbitMq` | Microsoft.Extensions.Configuration, RabbitMQ.Client |
| `src/{Name}.Infrastructure/Messaging/RabbitMq/RabbitMqConstants.cs` | `{Name}.Infrastructure.Messaging.RabbitMq` | none |
| `src/{Name}.Infrastructure/Messaging/RabbitMq/RabbitMqModule.cs` | `{Name}.Infrastructure.Messaging.RabbitMq` | Microsoft.Extensions.DependencyInjection |
| `src/{Name}.Infrastructure/Messaging/RabbitMq/RabbitMqPublisher.cs` | `{Name}.Infrastructure.Messaging.RabbitMq` | RabbitMQ.Client, {Name}.Shared.Domain |
| `src/{Name}.Infrastructure/Messaging/RabbitMq/ReceiveEndpointService.cs` | `{Name}.Infrastructure.Messaging.RabbitMq` | Microsoft.Extensions.DependencyInjection, Microsoft.Extensions.Hosting, Microsoft.Extensions.Logging, RabbitMQ.Client, RabbitMQ.Client.Events, System.Diagnostics, System.Globalization, {Name}.Infrastructure.Logging, {Name}.Infrastructure.Tenancy, {Name}.Shared.Diagnostics, {Name}.Shared.Domain, {Name}.Shared.Persistence |
| `test/{Name}.Tests/Infrastructure/Messaging/RabbitMq/MessageEnvelopeTests.cs` | `{Name}.Tests.Infrastructure.Messaging.RabbitMq` | System.Diagnostics, System.Text, {Name}.Contracts.Orders, {Name}.Domain.Aggregates.Orders.Events, {Name}.Infrastructure.Messaging.RabbitMq, {Name}.Tests.TestSupport.Shared.Diagnostics |
| `test/{Name}.Tests/Infrastructure/Messaging/RabbitMq/MessageNamesTests.cs` | `{Name}.Tests.Infrastructure.Messaging.RabbitMq` | {Name}.Contracts.Orders, {Name}.Domain.Aggregates.Orders.Events, {Name}.Infrastructure.Messaging.RabbitMq, {Name}.Tests.TestSupport.Infrastructure.Messaging |
