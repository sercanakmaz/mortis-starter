# Observability, C#

Applies when `addons` contains `observability`. Projects `{Name}.Shared` (folder `Diagnostics/`), `{Name}.Infrastructure` (folder `Diagnostics/`, and the messaging and logging files it changes) and the hosts. Layer 1: `architecture/http-and-operations.md`, `options/addons.md`.

**Built** in the advanced reference solution (decision 52; RabbitMQ, background outbox, separate Worker): compiled, tested and verified there. The in-process (`EventPublisher`) and database-transport lines follow the same pattern and are proved by `observability.sh` when such a project is generated.

Packages (versions in `Directory.Packages.props`), in `{Name}.Infrastructure`: `OpenTelemetry.Extensions.Hosting` 1.21.0, `OpenTelemetry.Exporter.OpenTelemetryProtocol` 1.21.0, `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` 10.0.15; in `{Name}.Api`: `OpenTelemetry.Instrumentation.AspNetCore` 1.21.0. The tests need none: a plain `ActivityListener` records spans. The shared kernel uses only `System.Diagnostics`, which needs no package.

## How it fits together

```
GET /health/live         MapHealthChecks, no checks: 200 while the process answers (/health stays, the same answer)
GET /health/ready        the checks tagged "ready": database (AddDbContextCheck), outbox lag (events.outbox != none),
                         broker (events.transport = rabbitmq); 503 and each check's status when one fails
spans (ActivitySource Telemetry.Source, named after the project):
  HTTP request             AddAspNetCoreInstrumentation, in the API
  command                  CommandPipeline.ExecuteAsync: one span named after the command; error status on an exception
  sync event handler       EventHandlerExecutor: one span per handler
  consumer (in-process)    EventPublisher: one span per consumer, a child of the request
  outbox publish           OutboxWorker: parent := OutboxMessage.TraceParent (the span that wrote the row)
  consumer (database)      OutboxWorker runs the consumer inside that span
  consumer (rabbitmq)      ReceiveEndpointService: parent := the envelope header "traceparent"
metrics (Meter Telemetry.Meter): commands.duration (ms; command, outcome), outbox.pending, outbox.oldest_age (s),
                         consumers.failures (consumer)
export                   OTLP when Observability:OtlpEndpoint is set; nothing otherwise
logs                     every JSON line gains TraceId and SpanId; RequestLoggingMiddleware writes one line per request
```

- The trace context is the W3C `traceparent` string (`Activity.Id`). It is stored with the outbox row and copied into the bus envelope, so a request and everything it caused share one trace id with every transport, across the API and the Worker.
- Without an exporter the SDK still listens to the project's source, so spans exist and every log line carries the trace id; nothing leaves the process.
- The Worker adds the same module without the ASP.NET instrumentation; it has no HTTP port, so it has no health endpoints: the API's readiness reports the outbox lag the Worker is responsible for.
- The Console does not add the module: a run is short and its log lines already carry the correlation id.
- Request logs carry the method, the route template (not the path or the query string, which can carry personal data), the status, the duration and the correlation id. Bodies are never logged; ASP.NET's own `AddHttpLogging` can add them on a developer's machine and is deliberately not generated.

## Files this add-on changes

| File | Change |
|---|---|
| `src/{Name}.Api/Program.cs` | `builder.Services.AddObservability(builder.Configuration, tracing => tracing.AddAspNetCoreInstrumentation());`; `app.UseMiddleware<RequestLoggingMiddleware>();` before `app.UseExceptionHandler();`; the two `MapHealthChecks` lines below |
| `src/{Name}.Worker/Program.cs` (hosts.worker = separate) | `builder.Services.AddObservability(builder.Configuration);` |
| `src/{Name}.Api/appsettings.json`, Worker's | `"Observability": { "OtlpEndpoint": "", "OutboxLagThresholdSeconds": 60 }` |
| `src/{Name}.Shared/Application/CommandPipeline.cs` | the command span and duration |
| `src/{Name}.Shared/Domain/EventHandlerExecutor.cs` | a span per sync handler |
| `src/{Name}.Infrastructure/Messaging/EventPublisher.cs` (events.outbox = none) | a span per consumer, the failure counter |
| `src/{Name}.Infrastructure/Messaging/Outbox/OutboxMessage.cs`, `OutboxMessageConfiguration.cs`, `OutboxWorker.cs` | `TraceParent`; the publish span; with the database transport the consumer's failures; a migration adds `outbox_messages.trace_parent` |
| `src/{Name}.Infrastructure/Messaging/RabbitMq/MessageEnvelope.cs`, `ReceiveEndpointService.cs`, `RabbitMqConstants.cs` (using `System.Diagnostics` and `{Name}.Shared.Diagnostics` where a span starts) | the `traceparent` header; the consume span and the failure counter |
| `src/{Name}.Infrastructure/Logging/JsonLineFormatter.cs`, `LoggingConstants.cs` | `TraceId` and `SpanId` |

## Telemetry

File `src/{Name}.Shared/Diagnostics/Telemetry.cs`:

```csharp
public static class Telemetry
{
    public const string Name = "{Name}";
    public const string Succeeded = "succeeded";
    public const string Refused = "refused";
    public const string Failed = "failed";

    public static readonly ActivitySource Source = new(Name);
    public static readonly Meter Meter = new(Name);
    public static readonly Histogram<double> CommandDuration = Meter.CreateHistogram<double>("commands.duration", "ms", "Time a command took, by command and outcome.");
    public static readonly Counter<long> ConsumerFailures = Meter.CreateCounter<long>("consumers.failures", description: "Event consumer runs that threw, by consumer.");
}
```

`refused` is a failure the caller caused (an `ExceptionBase` below 500: validation, not found, a business rule); `failed` is everything else. Dashboards alert on `failed`, not on `refused`.

## The command span

`CommandPipeline.ExecuteAsync` keeps its body as a private `RunAsync` and wraps it (usings `{Name}.Shared.Diagnostics`, `System.Diagnostics`, `System.Net`):

```csharp
    public async Task<CommandResult<TContent>> ExecuteAsync<TContent>(TCommand command, Func<TCommand, Task<CommandResult<TContent>>> onHandle)
    {
        using var activity = Telemetry.Source.StartActivity(typeof(TCommand).Name);
        var startedAt = Stopwatch.GetTimestamp();
        var outcome = Telemetry.Succeeded;

        try
        {
            return await RunAsync(command, onHandle);
        }
        catch (Exception exception)
        {
            outcome = exception is ExceptionBase { HttpStatusCode: < HttpStatusCode.InternalServerError } ? Telemetry.Refused : Telemetry.Failed;
            activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);

            throw;
        }
        finally
        {
            Telemetry.CommandDuration.Record(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds, new KeyValuePair<string, object>("command", typeof(TCommand).Name), new KeyValuePair<string, object>("outcome", outcome));
        }
    }
```

`EventHandlerExecutor` wraps each handler the same way, without the metric:

```csharp
            foreach (var eventHandler in eventHandlers)
            {
                using var activity = Telemetry.Source.StartActivity($"{eventHandler.GetType().Name} handles {@event.GetType().Name}");

                await eventHandler.Handle(@event);
            }
```

`EventPublisher` (no outbox) does the same per consumer and, in its catch, `Telemetry.ConsumerFailures.Add(1, new KeyValuePair<string, object>("consumer", consumerType.Name));`.

## The outbox and the bus

`OutboxMessage` gains `public string TraceParent { get; private set; }`, set in `From` with `TraceParent = Activity.Current?.Id`; `OutboxMessageConfiguration` gives it `HasMaxLength(OutboxSettings.TraceParentMaxLength)` (`public const int TraceParentMaxLength = 55;` in `OutboxSettings`, the length of a W3C `traceparent`). Regenerate the `Initial` migration (`infrastructure-postgres.md`).

`OutboxWorker.PublishAsync`, right after the event is read from the row:

```csharp
        ActivityContext.TryParse(message.TraceParent, null, out var parent);
        using var activity = Telemetry.Source.StartActivity($"publish {@event.GetType().Name}", ActivityKind.Producer, parent);
```

A row written before the add-on, or by a process without it, has no trace parent: `TryParse` fails, `parent` stays `default`, and the span starts a new trace. With the database transport the consumer runs inside this span, and the catch adds to `ConsumerFailures`.

`MessageEnvelope.From` adds the header, so every published envelope carries the current span:

```csharp
                [RabbitMqConstants.TraceParentHeader] = Activity.Current?.Id
```

`RabbitMqConstants.TraceParentHeader` is `"traceparent"`. `ReceiveEndpointService`, after the event is read from the envelope:

```csharp
            ActivityContext.TryParse(envelope.Headers.GetValueOrDefault(RabbitMqConstants.TraceParentHeader)?.ToString(), null, out var parent);
            using var activity = Telemetry.Source.StartActivity($"consume {eventType.Name}", ActivityKind.Consumer, parent);
```

and both of its catch blocks add `Telemetry.ConsumerFailures.Add(1, new KeyValuePair<string, object>("consumer", eventConsumerEndpoint.ConsumerType.Name));`. A header read back from JSON is a `JsonElement`; its `ToString()` is the string value. A MassTransit publisher sends no such envelope header, so its messages start a new trace.

## Logs

`LoggingConstants` gains `TraceId = "TraceId"` and `SpanId = "SpanId"`; `JsonLineFormatter.Write` writes them after `Message`:

```csharp
            writer.WriteString(LoggingConstants.TraceId, Activity.Current?.TraceId.ToString());
            writer.WriteString(LoggingConstants.SpanId, Activity.Current?.SpanId.ToString());
```

The console logger formats on the calling thread, so `Activity.Current` is the span of the code that logged.

File `src/{Name}.Api/Middlewares/RequestLoggingMiddleware.cs`:

```csharp
public class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext httpContext)
    {
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            await next(httpContext);
        }
        finally
        {
            logger.LogInformation("{Method} {Route} answered {StatusCode} in {DurationMilliseconds} ms {CorrelationId}",
                httpContext.Request.Method,
                (httpContext.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? ObservabilityConstants.UnmatchedRoute,
                httpContext.Response.StatusCode,
                Math.Round(Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds, 1),
                httpContext.Items[ApiConstants.CorrelationIdItem]);
        }
    }
}
```

It runs first, outside the exception handler, so the status it logs is the one the client received.

## Health

File `src/{Name}.Infrastructure/Diagnostics/ObservabilityModule.cs`, shown with an outbox and RabbitMQ (drop the lines of an option that is off):

```csharp
public static class ObservabilityModule
{
    public static IServiceCollection AddObservability(this IServiceCollection services, IConfiguration configuration, Action<TracerProviderBuilder> hostTracing = null)
    {
        var settings = configuration.GetSection(ObservabilitySettings.Section).Get<ObservabilitySettings>() ?? new ObservabilitySettings();

        services.Configure<ObservabilitySettings>(configuration.GetSection(ObservabilitySettings.Section));
        services.AddSingleton<OutboxLagProbe>();
        services.AddHostedService(serviceProvider => serviceProvider.GetRequiredService<OutboxLagProbe>());
        services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>(ObservabilityConstants.DatabaseCheck, tags: [ObservabilityConstants.ReadyTag])
            .AddCheck<OutboxHealthCheck>(ObservabilityConstants.OutboxCheck, tags: [ObservabilityConstants.ReadyTag])
            .AddCheck<RabbitMqHealthCheck>(ObservabilityConstants.BrokerCheck, tags: [ObservabilityConstants.ReadyTag]);

        var openTelemetry = services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(Telemetry.Name))
            .WithTracing(tracing =>
            {
                tracing.AddSource(Telemetry.Name);
                hostTracing?.Invoke(tracing);
            })
            .WithMetrics(metrics => metrics.AddMeter(Telemetry.Name));

        if (!string.IsNullOrWhiteSpace(settings.OtlpEndpoint))
        {
            openTelemetry.UseOtlpExporter(OtlpExportProtocol.Grpc, new Uri(settings.OtlpEndpoint));
        }

        return services;
    }
}
```

`ObservabilitySettings` holds `OtlpEndpoint` and `OutboxLagThresholdSeconds` (60), section `Observability`. `ObservabilityConstants` holds the paths `/health/live` and `/health/ready`, the tag `ready`, the check names `database`, `outbox` and `broker`, and `UnmatchedRoute = "(no route)"`.

File `src/{Name}.Infrastructure/Diagnostics/OutboxLagProbe.cs`, the one place that measures the outbox:

```csharp
public class OutboxLagProbe(IServiceScopeFactory serviceScopeFactory, ILogger<OutboxLagProbe> logger) : BackgroundService
{
    private const int RefreshSeconds = 15;

    public int Pending { get; private set; }
    public double OldestAgeSeconds { get; private set; }

    public void Register()
    {
        Telemetry.Meter.CreateObservableGauge("outbox.pending", () => Pending, description: "Outbox rows not published yet.");
        Telemetry.Meter.CreateObservableGauge("outbox.oldest_age", () => OldestAgeSeconds, "s", "Age of the oldest outbox row not published yet.");
    }

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceScopeFactory.CreateScope();
        var outboxMessages = scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<OutboxMessage>().AsNoTracking();

        Pending = await outboxMessages.CountAsync(cancellationToken);
        var oldest = await outboxMessages.MinAsync(message => (DateTime?)message.OccurredAt, cancellationToken);
        OldestAgeSeconds = oldest is { } occurredAt ? (DateTime.UtcNow - occurredAt).TotalSeconds : 0;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Register();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshAsync(stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(exception, "Measuring the outbox failed; readiness reports it, and the probe tries again");
            }

            await Task.Delay(TimeSpan.FromSeconds(RefreshSeconds), stoppingToken);
        }
    }
}
```

A failed measurement never stops the host (an exception escaping a `BackgroundService` would): the probe logs and tries again, and readiness reports the outbox as unhealthy. With tenancy the outbox has no tenant filter (it is not `IMultiTenant`), so the probe sees every tenant's rows.

File `src/{Name}.Infrastructure/Diagnostics/OutboxHealthCheck.cs`:

```csharp
public class OutboxHealthCheck(OutboxLagProbe outboxLagProbe, IOptions<ObservabilitySettings> settings) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await outboxLagProbe.RefreshAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("The outbox cannot be measured.", exception);
        }

        return outboxLagProbe.OldestAgeSeconds > settings.Value.OutboxLagThresholdSeconds
            ? HealthCheckResult.Unhealthy($"The oldest unpublished event is {outboxLagProbe.OldestAgeSeconds:F0} s old.")
            : HealthCheckResult.Healthy();
    }
}
```

File `src/{Name}.Infrastructure/Diagnostics/RabbitMqHealthCheck.cs`: opens and closes a channel through `RabbitMqConnection` (`await using var channel = await rabbitMqConnection.CreateChannelAsync(false, cancellationToken);`), healthy when that succeeds and unhealthy with the exception otherwise. The API never publishes with the background outbox, so opening a channel is the only way to know it could.

The API, after `app.MapGet("/health", ...)`:

```csharp
app.MapHealthChecks(ObservabilityConstants.LivePath, new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks(ObservabilityConstants.ReadyPath, new HealthCheckOptions { Predicate = check => check.Tags.Contains(ObservabilityConstants.ReadyTag), ResponseWriter = HealthResponse.WriteAsync });
```

File `src/{Name}.Api/Observability/HealthResponse.cs`:

```csharp
public static class HealthResponse
{
    public static Task WriteAsync(HttpContext httpContext, HealthReport healthReport)
    {
        return httpContext.Response.WriteAsJsonAsync(new
        {
            Status = healthReport.Status.ToString(),
            Checks = healthReport.Entries.ToDictionary(entry => entry.Key, entry => entry.Value.Status.ToString())
        });
    }
}
```

Health endpoints are not MVC endpoints, so the global `AuthorizeFilter` does not apply: they answer without a token, like `/health`. The response names each check and its status, never the exception, which may carry a connection string.

## Tests

`test/{Name}.Tests/TestSupport/Shared/Diagnostics/RecordedActivities.cs` records the spans of the project's source and of a test source. It is an instance with a `TestSource`, not a static one: a static `ActivitySource` field in a class that also listens fails its type initializer.

@@members variant:advanced/test/Boilerplate.Tests/TestSupport/Shared/Diagnostics/RecordedActivities.cs::RecordedActivities@@

`CommandPipelineTests` gains:

@@member variant:advanced/test/Boilerplate.Tests/Shared/Application/CommandPipelineTests.cs::CommandPipelineTests::ExecuteAsync_Should_RecordOneSpanNamedAfterTheCommandWithErrorStatusOnFailure@@

With an outbox, `OutboxMessageTests` gains:

@@member variant:advanced/test/Boilerplate.Tests/Infrastructure/Messaging/Outbox/OutboxMessageTests.cs::OutboxMessageTests::From_InsideAnActivity_Should_StoreThatActivitysIdAsTraceParent@@

@@member variant:advanced/test/Boilerplate.Tests/Infrastructure/Messaging/Outbox/OutboxMessageTests.cs::OutboxMessageTests::From_OutsideAnyActivity_Should_StoreNoTraceParent@@

With RabbitMQ, `MessageEnvelopeTests` gains:

@@member variant:advanced/test/Boilerplate.Tests/Infrastructure/Messaging/RabbitMq/MessageEnvelopeTests.cs::MessageEnvelopeTests::From_InsideAnActivity_Should_CarryItsIdInTheTraceParentHeader@@

Each of these test files adds the usings `System.Diagnostics` and `{Name}.Tests.TestSupport.Shared.Diagnostics`. The link from a request to the consumer's command, across the API and the Worker, is proved by `scripts/conformance/observability.sh` through the `TraceId` of the log lines, for every transport; there is no in-process trace test.

## Namespaces and usings

| File | Namespace | Usings |
|---|---|---|
| `Telemetry.cs` | `{Name}.Shared.Diagnostics` | `System.Diagnostics`, `System.Diagnostics.Metrics` |
| `ObservabilityModule.cs` | `{Name}.Infrastructure.Diagnostics` | `{Name}.Infrastructure.Persistence`, `{Name}.Shared.Diagnostics`, `Microsoft.Extensions.Configuration`, `Microsoft.Extensions.DependencyInjection`, `OpenTelemetry`, `OpenTelemetry.Exporter`, `OpenTelemetry.Metrics`, `OpenTelemetry.Resources`, `OpenTelemetry.Trace` |
| `OutboxLagProbe.cs`, `OutboxHealthCheck.cs`, `RabbitMqHealthCheck.cs` | `{Name}.Infrastructure.Diagnostics` | `{Name}.Infrastructure.Messaging.Outbox`, `{Name}.Infrastructure.Messaging.RabbitMq`, `{Name}.Infrastructure.Persistence`, `{Name}.Shared.Diagnostics`, `Microsoft.EntityFrameworkCore`, `Microsoft.Extensions.Diagnostics.HealthChecks`, `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Options` |
| `RequestLoggingMiddleware.cs` | `{Name}.Api.Middlewares` | `{Name}.Infrastructure.Diagnostics`, `System.Diagnostics` |
| `ObservabilitySettings.cs`, `ObservabilityConstants.cs` | `{Name}.Infrastructure.Diagnostics` | none |
| `HealthResponse.cs` | `{Name}.Api.Observability` | `Microsoft.Extensions.Diagnostics.HealthChecks` |
| `RecordedActivities.cs` | `{Name}.Tests.TestSupport.Shared.Diagnostics` | `System.Diagnostics`, `{Name}.Shared.Diagnostics` |
| `Program.cs` (API) | | adds `{Name}.Api.Observability`, `{Name}.Infrastructure.Diagnostics`, `Microsoft.AspNetCore.Diagnostics.HealthChecks`, `OpenTelemetry.Trace` |

## Decision record

`docs/adr/0022-health-traces-and-metrics.md`, in `project-docs-adr.md`.
