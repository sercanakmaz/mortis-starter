# Multi-tenancy by interface, C#

Applies when `tenancy.mode` is `interface`; it needs `auth` = `jwt`, because the tenant is the signed-in user's `tenant_id` claim (`authentication.md`). This page is the `tenancy.key` = `guid` code; `text` is at the end. Layer 1: `options/tenancy.md`, `architecture/tenancy.md`.

One database shared by all tenants. A type becomes tenant-owned by implementing `IMultiTenant`; everything else stays global and visible to every tenant. No database or schema per tenant.

## How it fits together

```
Api request     ContextCommandFilter: CommandContext.TenantId := the tenant claim; tenantScope.Enter(it)
Console run     the token's tenant claim (no token: no tenant); tenantScope.Enter(it)
event consumer  new DI scope; tenantScope.Enter(event.TenantId) BEFORE any other service is resolved
command         TenantCommandHandlerMiddleware: tenantScope.Enter(command.Context.TenantId); another tenant than the scope's -> 403
read            every IMultiTenant type has a query filter: tenant_id = the scope's tenant; no tenant -> 403 tenant_required, before any SQL
write           SaveChanges: a new IMultiTenant row gets the scope's tenant; changing or deleting another tenant's row -> 403
```

- `TenantScope` is the one ambient value of the design: one per dependency-injection scope, set by the host or the consumer, read by `AppDbContext`. Domain and Application never read it; they carry the tenant on the context and the events.
- A tenant-owned query without a tenant fails; it never returns every tenant's rows.
- Every unique index on a tenant-owned table starts with `tenant_id`, and a foreign key between tenant-owned tables includes it, so a row can never point at another tenant's row.
- A child entity of a tenant-owned aggregate is tenant-owned too and gets the same tenant.
- An event carries the tenant of the command that raised it (`EventContext`); the outbox row stores it in `tenant_id`, and the RabbitMQ envelope in the `Mortis-Tenant-Id` header.
- `TenantScope.Bypass(reason)` lifts the filter for a system job (cleanup, a report across tenants), logs a warning, and ends when disposed. It is for Infrastructure and the Worker only. `IgnoreQueryFilters()` is never used.
- Raw SQL and bulk updates (`ExecuteUpdate`, `FromSql`) skip the filter: write the `tenant_id` condition yourself, or keep them to global tables (the outbox).

## IMultiTenant

File `src/{Name}.Shared/Domain/IMultiTenant.cs`:

```csharp
public interface IMultiTenant
```

```csharp
public interface IMultiTenant
```

```csharp
Guid TenantId { get; }
```

A tenant-owned type implements it with a property the database sets, never a constructor argument:

```csharp
public class Product : AggregateRoot<Guid>, IMultiTenant
{
    public Guid TenantId { get; private set; }
```

In the example (`example-domain.md`, "when tenancy is on"): `Product`, `Customer`, `Order` and its `OrderLine` are tenant-owned; `Category` stays global.

## TenantRequiredException

File `src/{Name}.Shared/Exceptions/TenantRequiredException.cs`, answered 403 with code `tenant_required` (`failures.md`):

```csharp
public class TenantRequiredException(string type) : ExceptionBase("tenant_required", HttpStatusCode.Forbidden, $"{type} belongs to a tenant, and the caller has none.", parameters: new Dictionary<string, object> { ["type"] = type });
```

With `exceptions` = `catalogued`, `ErrorCatalogue` gets `tenant_required` (with its message `"The record belongs to a tenant, and the caller has none."`, or in the message files with `languages`).

## TenantScope

File `src/{Name}.Infrastructure/Tenancy/TenantScope.cs`, registered scoped by `AddInfrastructureModule`:

```csharp
public class TenantScope(ILogger<TenantScope> logger)
```

```csharp
public class TenantScope(ILogger<TenantScope> logger)
```

```csharp
public Guid? TenantId { get; private set; }
```

```csharp
public bool Bypassed { get; private set; }
```

```csharp
public void Enter(string tenantId)
{
    if (string.IsNullOrEmpty(tenantId)) return;

    if (!Guid.TryParse(tenantId, out var parsed))
    {
        throw new ForbiddenException("A tenant that is not a valid id");
    }

    if (TenantId is not null && TenantId != parsed)
    {
        throw new ForbiddenException("Switching tenant inside one scope");
    }

    TenantId = parsed;
}
```

```csharp
public Guid Required(string type)
{
    if (Bypassed) return TenantId ?? Guid.Empty;

    return TenantId ?? throw new TenantRequiredException(type);
}
```

```csharp
public IDisposable Bypass(string reason)
{
    logger.LogWarning("The tenant filter is bypassed: {Reason}", reason);

    Bypassed = true;

    return new BypassRelease(this);
}
```

```csharp
private sealed class BypassRelease(TenantScope tenantScope) : IDisposable
{
    public void Dispose()
    {
        tenantScope.Bypassed = false;
    }
}
```

- A tenant that is not a valid id, or a second tenant in the same scope, is refused with 403 `forbidden`.

## TenantCommandHandlerMiddleware

File `src/{Name}.Infrastructure/Tenancy/TenantCommandHandlerMiddleware.cs`, registered after the logging middleware. It makes the command's own context the source of truth inside the pipeline:

```csharp
public class TenantCommandHandlerMiddleware(TenantScope tenantScope) : ICommandHandlerMiddleware
```

```csharp
public class TenantCommandHandlerMiddleware(TenantScope tenantScope) : ICommandHandlerMiddleware
```

```csharp
public void Before(ICommand command)
{
    if (command is IContextCommand contextCommand)
    {
        tenantScope.Enter(contextCommand.Context?.TenantId);
    }
}
```

```csharp
public void After(ICommand command, CommandResult commandResult)
{
}
```

`AddInfrastructureModule` gains:

```csharp
        services.AddScoped<ICommandHandlerMiddleware, TenantCommandHandlerMiddleware>();
        services.AddScoped<TenantScope>();
```

## AppDbContext

`AppDbContext` (`infrastructure-postgres.md`) takes the `TenantScope`. It filters every `IMultiTenant` type and stamps new rows:

```csharp
public class AppDbContext(DbContextOptions<AppDbContext> options, TenantScope tenantScope) : DbContext(options)
```

```csharp
public class AppDbContext(DbContextOptions<AppDbContext> options, TenantScope tenantScope) : DbContext(options)
```

```csharp
private static readonly MethodInfo FilterByTenantMethod = typeof(AppDbContext).GetMethod(nameof(FilterByTenant), BindingFlags.NonPublic | BindingFlags.Instance);
```

```csharp
private bool TenantBypassed => tenantScope.Bypassed;
```

```csharp
private Guid CurrentTenantId => tenantScope.Required(PersistenceConstants.TenantOwnedData);
```

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

    foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(entityType => typeof(IMultiTenant).IsAssignableFrom(entityType.ClrType)).ToList())
    {
        FilterByTenantMethod.MakeGenericMethod(entityType.ClrType).Invoke(this, [modelBuilder]);
    }
}
```

```csharp
public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
{
    foreach (var entry in ChangeTracker.Entries<IMultiTenant>().ToList())
    {
        var tenantId = tenantScope.Required(entry.Metadata.ClrType.Name);

        if (entry.State == EntityState.Added && entry.Entity.TenantId == Guid.Empty)
        {
            entry.Property(nameof(IMultiTenant.TenantId)).CurrentValue = tenantId;
        }
        else if (entry.State != EntityState.Unchanged && entry.Entity.TenantId != tenantId && !tenantScope.Bypassed)
        {
            throw new ForbiddenException($"Writing another tenant's {entry.Metadata.ClrType.Name}");
        }
    }

    return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
}
```

```csharp
private void FilterByTenant<TEntity>(ModelBuilder modelBuilder) where TEntity : class, IMultiTenant
{
    modelBuilder.Entity<TEntity>().Property(entity => entity.TenantId).IsRequired();
    modelBuilder.Entity<TEntity>().HasQueryFilter(entity => TenantBypassed || entity.TenantId == CurrentTenantId);
}
```

- The filter reads `CurrentTenantId` when the query runs, so a query without a tenant throws `TenantRequiredException` before EF sends SQL.
- `PersistenceConstants` gains `public const string TenantOwnedData = "Tenant-owned data";`, the name in that message.
- `DesignTimeDbContextFactory` passes `new TenantScope(NullLogger<TenantScope>.Instance)`.

## Keys and foreign keys

Each unique index and alternate key of a tenant-owned type starts with `TenantId`; a foreign key to another tenant-owned type names both columns:

```csharp
builder.HasAlternateKey(product => new { product.TenantId, product.Sku });
```

```csharp
builder.HasIndex(customer => new { customer.TenantId, customer.Email }).IsUnique();
builder.HasAlternateKey(customer => new { customer.TenantId, customer.Id });
```

```csharp
builder.HasIndex(order => new { order.TenantId, order.OrderNumber }).IsUnique();

builder.HasOne<Customer>()
    .WithMany()
    .HasForeignKey(order => new { order.TenantId, order.CustomerId })
    .HasPrincipalKey(customer => new { customer.TenantId, customer.Id })
    .OnDelete(DeleteBehavior.Restrict);
```

```csharp
builder.HasOne<Product>()
    .WithMany()
    .HasForeignKey(orderLine => new { orderLine.TenantId, orderLine.Sku })
    .HasPrincipalKey(product => new { product.TenantId, product.Sku })
    .OnDelete(DeleteBehavior.Restrict);
```

A foreign key from a tenant-owned type to a global one (`Product.CategoryId`) stays a single column.

## The hosts and the consumers

- Api: `ContextCommandFilter` sets `TenantId` and enters the scope (`authentication.md`), for reads and writes alike, since queries carry no context.
- Console: `Program.cs` enters the scope with the token's tenant before running the command (`authentication.md`).
- RabbitMQ consumers: `ReceiveEndpointService` enters the event's tenant first thing in the consumer's scope:

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
                logger.LogWarning(exception, "Event consumer {EventConsumer} failed for {Event} {EventId}, attempt {Attempt}", eventConsumerEndpoint.ConsumerType.FullName, eventType.Name, @event.EventId, attempt);
            }
            catch (Exception exception)
            {
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

- With `events.transport` = `in-process` or `database`, `EventPublisher` and `OutboxWorker` add the same line after creating each consumer's scope.
- The outbox row has `TenantId` (`OutboxSettings.TenantIdMaxLength` = 64), copied from the event in `OutboxMessage.From`; the outbox table itself is not tenant-owned, so the worker sees every tenant's rows.

## Tests

`test/{Name}.Tests/Infrastructure/Tenancy/TenantScopeTests.cs`, with `TenantScopeBuilder` in `TestSupport/Infrastructure/Tenancy/`:

```csharp
public class TenantScopeTests
```

```csharp
[Fact]
public void Enter_WithAnId_Should_MakeItTheTenantOfTheScope()
{
    var tenantId = Guid.CreateVersion7();

    var tenantScope = TenantScopeBuilder.Create().WithTenant(tenantId).Build();

    Assert.Equal(tenantId, tenantScope.Required("Product"));
}
```

```csharp
[Fact]
public void Required_WithoutATenant_Should_ThrowTenantRequired()
{
    var tenantScope = TenantScopeBuilder.Create().Build();

    tenantScope.Enter(null);

    Assert.Equal("tenant_required", Assert.Throws<TenantRequiredException>(() => tenantScope.Required("Product")).Code);
}
```

```csharp
[Fact]
public void Enter_WithAValueThatIsNotAnId_Should_ThrowForbidden()
{
    var tenantScope = TenantScopeBuilder.Create().Build();

    Assert.Throws<ForbiddenException>(() => tenantScope.Enter("not-a-guid"));
}
```

```csharp
[Fact]
public void Enter_WithAnotherTenant_Should_ThrowForbidden_And_WithTheSameTenant_Should_Pass()
{
    var tenantId = Guid.CreateVersion7();
    var tenantScope = TenantScopeBuilder.Create().WithTenant(tenantId).Build();

    tenantScope.Enter(tenantId.ToString());

    Assert.Throws<ForbiddenException>(() => tenantScope.Enter(Guid.CreateVersion7().ToString()));
}
```

```csharp
[Fact]
public void Bypass_Should_LetRequiredPassWithoutATenantLogAWarningAndEndWhenDisposed()
{
    var tenantScopeBuilder = TenantScopeBuilder.Create();
    var tenantScope = tenantScopeBuilder.Build();

    using (tenantScope.Bypass("nightly cleanup"))
    {
        Assert.Equal(Guid.Empty, tenantScope.Required("Product"));
    }

    Assert.Throws<TenantRequiredException>(() => tenantScope.Required("Product"));
    Assert.Single(tenantScopeBuilder.BuildLogger().Entries, logEntry => logEntry.Level == LogLevel.Warning);
}
```

```csharp
public class TenantScopeBuilder
```

```csharp
private readonly RecordingLogger<TenantScope> _logger = new();
```

```csharp
private string _tenantId;
```

```csharp
public static TenantScopeBuilder Create() => new();
```

```csharp
public TenantScopeBuilder WithTenant(Guid tenantId)
{
    _tenantId = tenantId.ToString();
    return this;
}
```

```csharp
public RecordingLogger<TenantScope> BuildLogger() => _logger;
```

```csharp
public TenantScope Build()
{
    var tenantScope = new TenantScope(_logger);

    tenantScope.Enter(_tenantId);

    return tenantScope;
}
```

`test/{Name}.Tests/Infrastructure/Tenancy/TenantCommandHandlerMiddlewareTests.cs`:

```csharp
public class TenantCommandHandlerMiddlewareTests
```

```csharp
[Fact]
public void Before_WithAContextCommand_Should_EnterTheTenantOfItsContext()
{
    var tenantId = Guid.CreateVersion7();
    var tenantScope = TenantScopeBuilder.Create().Build();

    new TenantCommandHandlerMiddleware(tenantScope).Before(new FakeContextCommand { Context = new CommandContext { TenantId = tenantId.ToString() } });

    Assert.Equal(tenantId, tenantScope.TenantId);
}
```

```csharp
[Fact]
public void Before_WithAnotherTenantThanTheScope_Should_ThrowForbidden()
{
    var tenantScope = TenantScopeBuilder.Create().WithTenant(Guid.CreateVersion7()).Build();

    Assert.Throws<ForbiddenException>(() => new TenantCommandHandlerMiddleware(tenantScope).Before(new FakeContextCommand { Context = new CommandContext { TenantId = Guid.CreateVersion7().ToString() } }));
}
```

`test/{Name}.Tests/Infrastructure/Persistence/AppDbContextTests.cs`:

```csharp
public class AppDbContextTests
```

```csharp
[Fact]
public void Model_Should_FilterEveryTenantOwnedTypeAndNoGlobalOne()
{
    var model = AppDbContextBuilder.Create().Build().Model;

    Assert.All([typeof(Product), typeof(Customer), typeof(Order), typeof(OrderLine)], type => Assert.NotEmpty(model.FindEntityType(type).GetDeclaredQueryFilters()));
    Assert.Empty(model.FindEntityType(typeof(Category)).GetDeclaredQueryFilters());
}
```

```csharp
[Fact]
public async Task Query_OfATenantOwnedTypeWithoutATenant_Should_ThrowTenantRequiredBeforeAnySql()
{
    var context = AppDbContextBuilder.Create().Build();

    await Assert.ThrowsAsync<TenantRequiredException>(() => context.Set<Product>().ToListAsync());
}
```

```csharp
[Fact]
public async Task SaveChangesAsync_AddingATenantOwnedRowWithoutATenant_Should_ThrowTenantRequiredBeforeAnySql()
{
    var context = AppDbContextBuilder.Create().Build();

    context.Add(new Product(Guid.CreateVersion7(), "SKU-1", LocalizedText.Of("en", "Product"), 1));

    await Assert.ThrowsAsync<TenantRequiredException>(() => context.SaveChangesAsync());
}
```

```csharp
[Fact]
public async Task SaveChangesAsync_DeletingARowOfAnotherTenant_Should_ThrowForbidden()
{
    var context = AppDbContextBuilder.Create().WithTenantScope(TenantScopeBuilder.Create().WithTenant(Guid.CreateVersion7()).Build()).Build();
    var product = new Product(Guid.CreateVersion7(), "SKU-1", LocalizedText.Of("en", "Product"), 1);

    context.Attach(product);
    context.Remove(product);

    await Assert.ThrowsAsync<ForbiddenException>(() => context.SaveChangesAsync());
}
```

- `AppDbContextBuilder` takes a `TenantScope` (`WithTenantScope`); the configuration tests expect the tenant-first keys.
- `scripts/conformance/tenancy.sh` proves the rest against PostgreSQL: two tenants, the same sku in each, 404 across tenants, 403 without a tenant, a global category for everyone, the cross-tenant foreign key, consumers with interleaved tenants, and the tenant-first unique indexes.

## tenancy.key = text

The same code with `string` in place of `Guid`: `IMultiTenant.TenantId` is a `string` with a maximum length, `TenantScope` keeps the claim as it is (no parsing), and `Required` returns `string`. The tenant id is whatever the identity provider puts in the claim, so validate its format there.

## Decision record

`docs/adr/0015-multi-tenancy-by-interface.md`, in `project-docs-adr.md`.

## Namespaces and usings

Every file of this page, with its namespace and the `using` lines it needs (`ImplicitUsings` covers `System`, `System.Linq`, `System.Threading.Tasks` and the like). A file the table does not list follows the namespace rule in `naming.md`.

| File | Namespace | Usings |
|---|---|---|
| `src/{Name}.Infrastructure/Tenancy/TenantCommandHandlerMiddleware.cs` | `{Name}.Infrastructure.Tenancy` | {Name}.Shared.Application |
| `src/{Name}.Infrastructure/Tenancy/TenantScope.cs` | `{Name}.Infrastructure.Tenancy` | Microsoft.Extensions.Logging, {Name}.Shared.Exceptions |
| `src/{Name}.Shared/Domain/IMultiTenant.cs` | `{Name}.Shared.Domain` | none |
| `src/{Name}.Shared/Exceptions/TenantRequiredException.cs` | `{Name}.Shared.Exceptions` | System.Net |
| `test/{Name}.Tests/Infrastructure/Persistence/AppDbContextTests.cs` | `{Name}.Tests.Infrastructure.Persistence` | Microsoft.EntityFrameworkCore, {Name}.Domain.Aggregates.Categories, {Name}.Domain.Aggregates.Customers, {Name}.Domain.Aggregates.Orders, {Name}.Domain.Aggregates.Products, {Name}.Shared.Domain, {Name}.Shared.Exceptions, {Name}.Tests.TestSupport.Infrastructure.Persistence, {Name}.Tests.TestSupport.Infrastructure.Tenancy |
| `test/{Name}.Tests/Infrastructure/Tenancy/TenantCommandHandlerMiddlewareTests.cs` | `{Name}.Tests.Infrastructure.Tenancy` | {Name}.Infrastructure.Tenancy, {Name}.Shared.Application, {Name}.Shared.Exceptions, {Name}.Tests.TestSupport.Infrastructure.Tenancy, {Name}.Tests.TestSupport.Shared.Application |
| `test/{Name}.Tests/Infrastructure/Tenancy/TenantScopeTests.cs` | `{Name}.Tests.Infrastructure.Tenancy` | Microsoft.Extensions.Logging, {Name}.Shared.Exceptions, {Name}.Tests.TestSupport.Infrastructure.Tenancy |
| `test/{Name}.Tests/TestSupport/Infrastructure/Tenancy/TenantScopeBuilder.cs` | `{Name}.Tests.TestSupport.Infrastructure.Tenancy` | {Name}.Infrastructure.Tenancy, {Name}.Tests.TestSupport.Infrastructure.Messaging |
