# Testing levels, C#

Applies to the `testing` key: `unit` (the core), `handlers`, `integration`, `architecture`; each level includes the ones before it. Layer 1: `options/testing.md`. The core's test project, rules and test support are on `testing.md` and `testing-kernel.md`.

**Built** (decision 52): `handlers` and `integration` in the standard reference solution, `architecture` (with RabbitMQ and tenancy) in the advanced one; compiled, tested and verified there.

## What each level adds

| Level | Adds | Needs |
|---|---|---|
| `unit` | everything `testing.md` describes: domain, kernel, handler, host filter and mapping tests, all with fakes and builders, none touching I/O | nothing |
| `handlers` | a validator test per command; scenario tests that send a command through the real container, dispatcher, pipeline and validators, with only the persistence ports faked | nothing |
| `integration` | a second project `test/{Name}.IntegrationTests`: repositories, the unit of work, the outbox claim and (with RabbitMQ) the bus against a real PostgreSQL and broker, one fixture per collection, one database per test class | Docker, or `MORTIS_TEST_POSTGRES` / `MORTIS_TEST_RABBITMQ` |
| `architecture` | reflection rules (layer references, registrations, no private methods in a `*Service`), the event-contract golden file, a line-coverage gate | nothing |

- The core's handler tests stay at `unit`: they use no I/O, and every handler class has its test whatever the level. This differs from the plan's split (decision 49): the variants, which are the source of truth, ship handler tests in the lean preset.
- The house rules hold in the integration project: fixtures and builders in `TestSupport/`, test classes hold only tests. `testing.md`'s rule "no test talks to a database" is the unit project's rule; the integration project exists to break it.
- A test named on another page that needs PostgreSQL (`IdempotencyStoreTests`) lives in the integration project and exists only at `integration` and above.
- Every option's page lists its tests; a test that needs a database or broker belongs to `integration`, a reflection rule to `architecture`, anything else to the core.

## handlers

A validator test per command, `test/{Name}.Tests/Application/{Plural}/{Command}ValidatorTests.cs`:

```csharp
public class CreateProductCommandValidatorTests
{
    [Theory]
    [InlineData("", "Bolt", 1, "Sku")]
    [InlineData("SKU-1", "", 1, "Name")]
    [InlineData("SKU-1", "Bolt", -1, "Stock")]
    public void Validate_WithAnInvalidMember_Should_FailOnThatMember(string sku, string name, int stock, string member)
    {
        var validationResult = new CreateProductCommandValidator().Validate(new CreateProductCommand { Sku = sku, Name = name, Stock = stock });

        Assert.Contains(validationResult.Errors, validationFailure => validationFailure.PropertyName == member);
    }

    [Fact]
    public void Validate_WithAValidCommand_Should_Pass()
    {
        Assert.True(new CreateProductCommandValidator().Validate(new CreateProductCommand { Sku = "SKU-1", Name = "Bolt", Stock = 1 }).IsValid);
    }
}
```

File `test/{Name}.Tests/TestSupport/Scenarios/ScenarioHost.cs`, the real composition with the persistence ports replaced. Every repository and reader port of the Domain gets a default mock, so `ValidateOnBuild` can resolve every handler; `With` replaces the one a scenario watches (the last registration wins):

```csharp
public sealed class ScenarioHost
{
    private readonly ServiceCollection _services = new();

    private ScenarioHost()
    {
        _services.AddLogging();
        _services.AddSharedModule().AddDomainModule().AddApplicationModule();
        _services.AddScoped(_ => UnitOfWorkBuilder.Create().WithHasTransaction(true).Build());

        foreach (var port in typeof(DomainModule).Assembly.GetTypes().Where(type => type.IsInterface && (type.Name.EndsWith("Repository") || type.Name.EndsWith("Reader"))))
        {
            _services.AddScoped(port, _ => ((Mock)Activator.CreateInstance(typeof(Mock<>).MakeGenericType(port))).Object);
        }
    }

    public static ScenarioHost Create() => new();

    public ScenarioHost With<TPort>(TPort port) where TPort : class
    {
        _services.AddScoped(_ => port);

        return this;
    }

    public ICommandDispatcher Dispatcher() => _services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }).CreateScope().ServiceProvider.GetRequiredService<ICommandDispatcher>();
}
```

`ValidateOnBuild` makes a missing registration fail when the scenario starts, not at the first `Send`. A scenario, `test/{Name}.Tests/Scenarios/CreateProductScenarioTests.cs`:

```csharp
public class CreateProductScenarioTests
{
    [Fact]
    public async Task Send_WithAnInvalidCommand_Should_FailValidationBeforeAnyWrite()
    {
        var productRepositoryBuilder = ProductRepositoryBuilder.Create().WithCreateAsync();
        var commandDispatcher = ScenarioHost.Create().With(productRepositoryBuilder.Build()).Dispatcher();

        await Assert.ThrowsAsync<ValidationFailedException>(() => commandDispatcher.Send(new CreateProductCommand { Sku = "", Stock = -1 }));

        productRepositoryBuilder.BuildMock().Verify(productRepository => productRepository.CreateAsync(It.IsAny<Product>()), Times.Never);
    }

    [Fact]
    public async Task Send_Should_RunTheRegisteredHandlerAndWriteOnce()
    {
        var productRepositoryBuilder = ProductRepositoryBuilder.Create().WithCreateAsync();
        var commandDispatcher = ScenarioHost.Create().With(productRepositoryBuilder.Build()).Dispatcher();

        var commandResult = await commandDispatcher.Send(new CreateProductCommand { Sku = "SKU-1", Name = "Bolt", Stock = 1, Context = new CommandContext { User = new AuditUser("user-1", "user-1@localhost") } });

        Assert.Equal("SKU-1", commandResult.Content.Sku);
        productRepositoryBuilder.BuildMock().Verify(productRepository => productRepository.CreateAsync(It.IsAny<Product>()), Times.Once);
    }
}
```

One scenario per aggregate's main command is the floor; a scenario per event handler chain (a command whose sync handler sends another command) is where they pay most.

## integration

`test/{Name}.IntegrationTests/{Name}.IntegrationTests.csproj` references `{Name}.Infrastructure` and `{Name}.Application` and the packages `Microsoft.Extensions.Hosting` (configuration), `Testcontainers.PostgreSql` (4.17.0), `Testcontainers.RabbitMq` (with RabbitMQ), `Xunit.SkippableFact` (1.5.85), `Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`, and `<Using Include="Xunit" />`; it is added to `{Name}.slnx`, and its versions to `Directory.Packages.props`.

File `test/{Name}.IntegrationTests/TestSupport/PostgresFixture.cs`:

```csharp
public sealed class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer _container;

    public string ServerConnectionString { get; private set; }
    public string Unavailable { get; private set; }

    public async Task InitializeAsync()
    {
        ServerConnectionString = Environment.GetEnvironmentVariable(IntegrationConstants.PostgresVariable);

        if (ServerConnectionString == IntegrationConstants.SkipValue)
        {
            Unavailable = $"{IntegrationConstants.PostgresVariable} is {IntegrationConstants.SkipValue}.";
            return;
        }

        if (!string.IsNullOrEmpty(ServerConnectionString))
        {
            return;
        }

        try
        {
            _container = new PostgreSqlBuilder(IntegrationConstants.PostgresImage).Build();
            await _container.StartAsync();
            ServerConnectionString = _container.GetConnectionString();
        }
        catch (Exception exception) when (exception is DockerUnavailableException or HttpRequestException or TimeoutException or ArgumentException)
        {
            Unavailable = $"No PostgreSQL: set {IntegrationConstants.PostgresVariable} or start Docker ({exception.GetType().Name}).";
        }
    }

    public async Task<string> CreateDatabaseAsync(string testClass)
    {
        var database = $"{IntegrationConstants.DatabasePrefix}{testClass.ToLowerInvariant()}_{Guid.CreateVersion7():N}";
        var connectionString = new NpgsqlConnectionStringBuilder(ServerConnectionString) { Database = database }.ConnectionString;
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(connectionString).UseSnakeCaseNamingConvention().Options;

        await using var appDbContext = new AppDbContext(options);
        await appDbContext.Database.MigrateAsync();

        return connectionString;
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}
```

The options repeat `InfrastructureModule`'s (Npgsql and, with `naming.database` = `snake_case`, the naming convention) so the migrated schema matches. With tenancy, `AppDbContext`'s constructor takes the `TenantScope` as in `tenancy.md`. `IntegrationConstants` holds `PostgresVariable = "MORTIS_TEST_POSTGRES"`, `RabbitMqVariable = "MORTIS_TEST_RABBITMQ"`, the images (`postgres:16`, `rabbitmq:4-management`), `DatabasePrefix = "it_"`, `SkipValue = "skip"` (the variable set to `skip` reports every test skipped: how a check proves that a missing database never passes, since Testcontainers finds Docker's socket even when `DOCKER_HOST` points elsewhere) and the collection names `PostgresCollection = "postgres"`, `RabbitMqCollection = "rabbitmq"`. `verify.sh` runs the tests with `MORTIS_TEST_POSTGRES` built from the `PG*` variables (`lang/csharp.sh test`).

`PostgresCollection.cs` declares `[CollectionDefinition(IntegrationConstants.PostgresCollection)] public class PostgresCollection : ICollectionFixture<PostgresFixture>;` (and, with RabbitMQ, `RabbitMqCollection.cs` the same for `RabbitMqFixture`); one container serves the whole collection, and each test class makes its own database. `RabbitMqFixture.cs` is `PostgresFixture` for RabbitMQ (`RabbitMqBuilder`, `MORTIS_TEST_RABBITMQ`, an AMQP URI).

File `test/{Name}.IntegrationTests/TestSupport/IntegrationServices.cs`, the real container the way a host builds it, and the two helpers the tests use:

```csharp
public static class IntegrationServices
{
    public static ServiceProvider Create(string connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string> { [$"ConnectionStrings:{PersistenceConstants.ConnectionStringName}"] = connectionString })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSharedModule().AddDomainModule().AddApplicationModule().AddInfrastructureModule(configuration);

        return services.BuildServiceProvider();
    }

    public static async Task<TContent> Send<TContent>(this ServiceProvider serviceProvider, ICommand<TContent> command)
    {
        using var scope = serviceProvider.CreateScope();

        var commandResult = await scope.ServiceProvider.GetRequiredService<ICommandDispatcher>().Send(command);

        return commandResult.Content;
    }

    public static async Task<int> CountAsync<TEntity>(this ServiceProvider serviceProvider, Expression<Func<TEntity, bool>> predicate) where TEntity : class
    {
        using var scope = serviceProvider.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<TEntity>().IgnoreQueryFilters().CountAsync(predicate);
    }
}
```

A test, `test/{Name}.IntegrationTests/Persistence/ProductRepositoryTests.cs`:

```csharp
[Collection(IntegrationConstants.PostgresCollection)]
public class ProductRepositoryTests(PostgresFixture postgresFixture)
{
    [SkippableFact]
    public async Task Send_WithADuplicateSku_Should_ThrowDuplicateAndKeepOneRow()
    {
        Skip.If(postgresFixture.Unavailable is not null, postgresFixture.Unavailable);
        await using var serviceProvider = IntegrationServices.Create(await postgresFixture.CreateDatabaseAsync(nameof(ProductRepositoryTests)));

        await serviceProvider.Send(new CreateProductCommand { Sku = "SKU-1", Name = "Bolt", Stock = 1 });
        await Assert.ThrowsAsync<DuplicateException>(() => serviceProvider.Send(new CreateProductCommand { Sku = "SKU-1", Name = "Bolt again", Stock = 1 }));

        Assert.Equal(1, await serviceProvider.CountAsync<Product>(product => product.Sku == "SKU-1"));
    }
}
```

The primary constructor is the xUnit collection-fixture injection, not a field or a helper; it is the one constructor a test class may have. A missing PostgreSQL makes every test **skipped** with the reason, never passed.

`test/{Name}.IntegrationTests/Persistence/UnitOfWorkTests.cs`:

```csharp
[Collection(IntegrationConstants.PostgresCollection)]
public class UnitOfWorkTests(PostgresFixture postgresFixture)
{
    [SkippableFact]
    public async Task RollbackTransactionAsync_Should_LeaveNoRow()
    {
        Skip.If(postgresFixture.Unavailable is not null, postgresFixture.Unavailable);
        await using var serviceProvider = IntegrationServices.Create(await postgresFixture.CreateDatabaseAsync(nameof(UnitOfWorkTests)));

        using (var scope = serviceProvider.CreateScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await unitOfWork.BeginTransactionAsync(nameof(UnitOfWorkTests));
            await scope.ServiceProvider.GetRequiredService<IProductRepository>().CreateAsync(new Product(Guid.CreateVersion7(), "SKU-RB", "Rolled back", 1));
            await unitOfWork.RollbackTransactionAsync();
        }

        Assert.Equal(0, await serviceProvider.CountAsync<Product>(product => product.Sku == "SKU-RB"));
    }
}
```

With an outbox, `test/{Name}.IntegrationTests/Messaging/OutboxClaimTests.cs` and the rows it inserts, `TestSupport/OutboxRows.cs`:

```csharp
[Collection(IntegrationConstants.PostgresCollection)]
public class OutboxClaimTests(PostgresFixture postgresFixture)
{
    [SkippableFact]
    public async Task ClaimNextAsync_InTwoConcurrentTransactions_Should_ClaimDifferentRows()
    {
        Skip.If(postgresFixture.Unavailable is not null, postgresFixture.Unavailable);
        await using var serviceProvider = IntegrationServices.Create(await postgresFixture.CreateDatabaseAsync(nameof(OutboxClaimTests)));
        using var first = serviceProvider.CreateScope();
        using var second = serviceProvider.CreateScope();
        var firstContext = first.ServiceProvider.GetRequiredService<AppDbContext>();
        var secondContext = second.ServiceProvider.GetRequiredService<AppDbContext>();
        await firstContext.Database.ExecuteSqlRawAsync(OutboxRows.TwoPending);

        await using var firstTransaction = await firstContext.Database.BeginTransactionAsync();
        await using var secondTransaction = await secondContext.Database.BeginTransactionAsync();
        var firstClaim = await first.ServiceProvider.GetRequiredService<OutboxRepository>().ClaimNextAsync();
        var secondClaim = await second.ServiceProvider.GetRequiredService<OutboxRepository>().ClaimNextAsync();

        Assert.NotNull(firstClaim);
        Assert.NotNull(secondClaim);
        Assert.NotEqual(firstClaim.Id, secondClaim.Id);
    }
}
```

```csharp
public static class OutboxRows
{
    public const string TwoPending = """
        insert into outbox_messages (id, event_id, consumer, type, payload, occurred_at, attempts, max_attempts, next_attempt_at)
        values (gen_random_uuid(), gen_random_uuid(), 'probe', 'probe', jsonb_build_object(), now() at time zone 'utc' - interval '1 minute', 0, 5, now() at time zone 'utc' - interval '1 minute'),
               (gen_random_uuid(), gen_random_uuid(), 'probe', 'probe', jsonb_build_object(), now() at time zone 'utc' - interval '1 minute', 0, 5, now() at time zone 'utc' - interval '1 minute')
        """;
}
```

The SQL is the database transport's shape (with `consumer`); with RabbitMQ the row has no `consumer` column. `ExecuteSqlRawAsync` treats braces as format placeholders, so the payload is `jsonb_build_object()`, never `'{}'`. With RabbitMQ, `OutboxRows.TwoPending` has no `consumer` column:

```csharp
public static class OutboxRows
{
    public const string TwoPending = """
        insert into outbox_messages (id, type, payload, occurred_at, attempts, next_attempt_at)
        values (gen_random_uuid(), 'probe', jsonb_build_object(), now() at time zone 'utc' - interval '1 minute', 0, now() at time zone 'utc' - interval '1 minute'),
               (gen_random_uuid(), 'probe', jsonb_build_object(), now() at time zone 'utc' - interval '1 minute', 0, now() at time zone 'utc' - interval '1 minute')
        """;
}
```

With tenancy, `PostgresFixture` builds its context as `new AppDbContext(options, new TenantScope(NullLogger<TenantScope>.Instance))` (usings `{Name}.Infrastructure.Tenancy`, `Microsoft.Extensions.Logging.Abstractions`); `IntegrationServices` declares two tenants and enters one in every scope (`Send` and `Ask` take a `Guid tenantId`), and `CountAsync` reads with `IgnoreQueryFilters()` so a test sees every tenant's rows; with RabbitMQ, `Create` takes the broker URI as well:

```csharp
public static class IntegrationServices
{
    public static readonly Guid TenantA = Guid.Parse("0197a0a0-0000-7000-8000-00000000000a");
    public static readonly Guid TenantB = Guid.Parse("0197a0a0-0000-7000-8000-00000000000b");

    public static ServiceProvider Create(string connectionString, string rabbitMqUri = "amqp://guest:guest@localhost:5672")
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                [$"ConnectionStrings:{PersistenceConstants.ConnectionStringName}"] = connectionString,
                [$"ConnectionStrings:{RabbitMqConstants.ConnectionStringName}"] = rabbitMqUri
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSharedModule().AddDomainModule().AddApplicationModule().AddInfrastructureModule(configuration);

        return services.BuildServiceProvider();
    }

    public static async Task<TContent> Send<TContent>(this ServiceProvider serviceProvider, ICommand<TContent> command, Guid tenantId)
    {
        using var scope = serviceProvider.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantScope>().Enter(tenantId.ToString());

        var commandResult = await scope.ServiceProvider.GetRequiredService<ICommandDispatcher>().Send(command);

        return commandResult.Content;
    }

    public static async Task<TResult> Ask<TResult>(this ServiceProvider serviceProvider, IQuery<TResult> query, Guid tenantId)
    {
        using var scope = serviceProvider.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantScope>().Enter(tenantId.ToString());

        return await scope.ServiceProvider.GetRequiredService<IQueryDispatcher>().Ask(query);
    }

    public static async Task<int> CountAsync<TEntity>(this ServiceProvider serviceProvider, Expression<Func<TEntity, bool>> predicate) where TEntity : class
    {
        using var scope = serviceProvider.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<TEntity>().IgnoreQueryFilters().CountAsync(predicate);
    }
}
```

`UnitOfWorkTests` enters `TenantA` in its scope before writing. The cross-tenant test, in `ProductRepositoryTests`:

```csharp
    [SkippableFact]
    public async Task Send_WithTheSameSkuInTwoTenants_Should_KeepBothAndReadEachOnlyInItsTenant()
    {
        Skip.If(postgresFixture.Unavailable is not null, postgresFixture.Unavailable);
        await using var serviceProvider = IntegrationServices.Create(await postgresFixture.CreateDatabaseAsync(nameof(ProductRepositoryTests)));

        var productOfA = await serviceProvider.Send(new CreateProductCommand { Sku = "SKU-T", Name = "Bolt", Stock = 1 }, IntegrationServices.TenantA);
        await serviceProvider.Send(new CreateProductCommand { Sku = "SKU-T", Name = "Bolt", Stock = 1 }, IntegrationServices.TenantB);

        Assert.Equal(2, await serviceProvider.CountAsync<Product>(product => product.Sku == "SKU-T"));
        await Assert.ThrowsAsync<NotFoundException>(() => serviceProvider.Ask(new GetProductQuery { Id = productOfA.Id }, IntegrationServices.TenantB));
    }
```

With RabbitMQ, `TestSupport/RabbitMqFixture.cs` is `PostgresFixture`'s shape over `RabbitMqContainer` (`new RabbitMqBuilder(IntegrationConstants.RabbitMqImage)`, `GetConnectionString()`, the variable `MORTIS_TEST_RABBITMQ`, exposed as `Uri`); `RabbitMqCollection` is `[CollectionDefinition(IntegrationConstants.RabbitMqCollection)] public class RabbitMqCollection : ICollectionFixture<RabbitMqFixture>, ICollectionFixture<PostgresFixture>;`. `TestSupport/RabbitMqDeliveries.cs`:

```csharp
public static class RabbitMqDeliveries
{
    public static async Task<BasicGetResult> NextAsync(IChannel channel, string queue)
    {
        for (var attempt = 0; attempt < 50; attempt++)
        {
            var delivery = await channel.BasicGetAsync(queue, autoAck: true);

            if (delivery is not null)
            {
                return delivery;
            }

            await Task.Delay(100);
        }

        return null;
    }
}
```

`Messaging/RabbitMqRoundTripTests.cs` (the example's `LineAdded`; without the example, any `IQueueEvent`):

```csharp
[Collection(IntegrationConstants.RabbitMqCollection)]
public class RabbitMqRoundTripTests(RabbitMqFixture rabbitMqFixture, PostgresFixture postgresFixture)
{
    [SkippableFact]
    public async Task PublishAsync_Should_DeliverTheEnvelopeToAQueueBoundToTheEventsExchange()
    {
        Skip.If(rabbitMqFixture.Unavailable is not null, rabbitMqFixture.Unavailable);
        Skip.If(postgresFixture.Unavailable is not null, postgresFixture.Unavailable);
        await using var serviceProvider = IntegrationServices.Create(await postgresFixture.CreateDatabaseAsync(nameof(RabbitMqRoundTripTests)), rabbitMqFixture.Uri);
        var rabbitMqConnection = serviceProvider.GetRequiredService<RabbitMqConnection>();
        await using var channel = await rabbitMqConnection.CreateChannelAsync(false, CancellationToken.None);
        var exchange = MessageNames.Exchange(typeof(LineAdded));
        await channel.ExchangeDeclareAsync(exchange, ExchangeType.Fanout, durable: true, autoDelete: false);
        var queue = (await channel.QueueDeclareAsync(string.Empty, durable: false, exclusive: true, autoDelete: true)).QueueName;
        await channel.QueueBindAsync(queue, exchange, string.Empty);

        await serviceProvider.GetRequiredService<RabbitMqPublisher>().PublishAsync(new LineAdded("order-1", "line-1", "SKU-1", 2), CancellationToken.None);

        var delivery = await RabbitMqDeliveries.NextAsync(channel, queue);
        Assert.NotNull(delivery);
        Assert.Equal(RabbitMqConstants.ContentType, delivery.BasicProperties.ContentType);
        Assert.Contains("SKU-1", System.Text.Encoding.UTF8.GetString(delivery.Body.Span));
    }
}
```

The error-queue path is proved by `rabbitmq.sh`, not by a test.

## architecture

`test/{Name}.Tests/Architecture/LayerRulesTests.cs`:

```csharp
public class LayerRulesTests
{
    [Theory]
    [InlineData(typeof(SharedModule), new string[0])]
    [InlineData(typeof(DomainModule), new[] { nameof(Shared) })]
    [InlineData(typeof(ApplicationModule), new[] { nameof(Shared), nameof(Domain) })]
    public void Assembly_Should_ReferenceOnlyTheLayersBelowIt(Type moduleType, string[] allowedLayers)
    {
        var projectPrefix = typeof(SharedModule).Namespace!.Split('.')[0] + ".";

        var referencedLayers = moduleType.Assembly.GetReferencedAssemblies()
            .Select(assemblyName => assemblyName.Name!)
            .Where(name => name.StartsWith(projectPrefix))
            .Select(name => name[projectPrefix.Length..]);

        Assert.Empty(referencedLayers.Except(allowedLayers));
    }
}
```

`nameof(Shared)` and `nameof(Domain)` resolve to the namespaces `{Name}.Shared` and `{Name}.Domain`; the inline arrays are constants, as attributes require.

`test/{Name}.Tests/Architecture/RegistrationRulesTests.cs`, closing the gap ADR `0001` names (a missing registration otherwise fails only at the first `Send`); the query test only when `queries` is not `inline`:

```csharp
public class RegistrationRulesTests
{
    [Fact]
    public void EveryCommand_Should_HaveARegisteredHandler()
    {
        var registered = new ServiceCollection().AddApplicationModule().Select(descriptor => descriptor.ServiceType).ToHashSet();

        var missing = typeof(ApplicationModule).Assembly.GetTypes()
            .Where(type => !type.IsAbstract)
            .SelectMany(type => type.GetInterfaces().Where(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(ICommand<>)), (type, contract) => (type, content: contract.GetGenericArguments()[0]))
            .Where(command => !registered.Contains(typeof(BaseCommandHandler<,>).MakeGenericType(command.type, command.content)))
            .Select(command => command.type.Name);

        Assert.Empty(missing);
    }

    [Fact]
    public void EveryQuery_Should_HaveARegisteredHandler()
    {
        var registered = new ServiceCollection().AddApplicationModule().Select(descriptor => descriptor.ServiceType).ToHashSet();

        var missing = typeof(ApplicationModule).Assembly.GetTypes()
            .Where(type => !type.IsAbstract)
            .SelectMany(type => type.GetInterfaces().Where(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IQuery<>)), (type, contract) => (type, result: contract.GetGenericArguments()[0]))
            .Where(query => !registered.Contains(typeof(BaseQueryHandler<,>).MakeGenericType(query.type, query.result)))
            .Select(query => query.type.Name);

        Assert.Empty(missing);
    }

    [Fact]
    public void EverySyncEventHandler_Should_BeRegistered()
    {
        var registered = new ServiceCollection().AddApplicationModule().Select(descriptor => descriptor.ImplementationType).ToHashSet();

        var missing = typeof(ApplicationModule).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(ISyncEventHandler).IsAssignableFrom(type) && !registered.Contains(type))
            .Select(type => type.Name);

        Assert.Empty(missing);
    }
}
```

Event consumers are registered by the hosts (`event-consumers.md`); the example's conformance proves theirs.

`test/{Name}.Tests/Architecture/ServiceRulesTests.cs`:

```csharp
public class ServiceRulesTests
{
    [Theory]
    [InlineData(typeof(DomainModule))]
    [InlineData(typeof(ApplicationModule))]
    public void Service_Should_DeclareNoPrivateMethod(Type moduleType)
    {
        var privateMethods = moduleType.Assembly.GetTypes()
            .Where(type => type.Name.EndsWith("Service") && type.GetCustomAttribute<CompilerGeneratedAttribute>() is null)
            .SelectMany(type => type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Where(method => method.IsPrivate && !method.IsSpecialName && method.GetCustomAttribute<CompilerGeneratedAttribute>() is null)
            .Select(method => $"{method.DeclaringType!.Name}.{method.Name}");

        Assert.Empty(privateMethods);
    }
}
```

`test/{Name}.Tests/Architecture/EventContractTests.cs`:

```csharp
public class EventContractTests
{
    [Fact]
    public void EveryQueueEvent_Should_MatchItsPinnedContract()
    {
        var pinned = EventContracts.ReadPinned();

        var current = EventContracts.Describe();

        Assert.Equal(pinned, current);
    }
}
```

`test/{Name}.Tests/TestSupport/Architecture/EventContracts.cs`; the wire name is `MessageNames.Exchange` with RabbitMQ and the type's `FullName` otherwise (then drop the `{Name}.Infrastructure.Messaging.RabbitMq` using). The golden file sits beside the test that reads it (`[CallerFilePath]`), so nothing is copied to the output:

```csharp
public static class EventContracts
{
    public const string UpdateVariable = "MORTIS_UPDATE_CONTRACTS";
    public const string FileName = "event-contracts.json";

    public static string Describe()
    {
        var contracts = new SortedDictionary<string, SortedDictionary<string, string>>(StringComparer.Ordinal);

        foreach (var eventType in typeof(DomainModule).Assembly.GetTypes().Where(type => !type.IsAbstract && typeof(IQueueEvent).IsAssignableFrom(type)))
        {
            contracts[MessageNames.Exchange(eventType)] = new SortedDictionary<string, string>(
                eventType.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(property => property.CanRead).ToDictionary(property => property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? property.Name, property => TypeName(property.PropertyType)),
                StringComparer.Ordinal);
        }

        return JsonSerializer.Serialize(contracts, new JsonSerializerOptions { WriteIndented = true }).ReplaceLineEndings("\n");
    }

    public static string ReadPinned([CallerFilePath] string callerPath = "")
    {
        var path = Path.Combine(Path.GetDirectoryName(callerPath)!, FileName);

        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
        {
            File.WriteAllText(path, Describe() + "\n");
        }

        return File.Exists(path) ? File.ReadAllText(path).ReplaceLineEndings("\n").TrimEnd('\n') : string.Empty;
    }

    public static string TypeName(Type type) => Nullable.GetUnderlyingType(type) is { } underlying ? TypeName(underlying) + "?" : type.IsGenericType ? $"{type.Name[..type.Name.IndexOf('`')]}<{string.Join(",", type.GetGenericArguments().Select(TypeName))}>" : type.Name;
}
```

Create the golden file `test/{Name}.Tests/Architecture/event-contracts.json` by running the tests once with `MORTIS_UPDATE_CONTRACTS=1`; never write it by hand. Changing a contract later is a deliberate, reviewed change to that file.

The coverage gate, in `{Name}.Tests.csproj`: the package `coverlet.msbuild` (10.1.1, `PrivateAssets="all"`) and

```xml
  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <CollectCoverage>true</CollectCoverage>
    <Threshold>80</Threshold>
    <ThresholdType>line</ThresholdType>
    <ThresholdStat>total</ThresholdStat>
    <CoverletOutput>bin/coverage/</CoverletOutput>
    <Include>[{Name}.Shared]*,[{Name}.Domain]*,[{Name}.Application]*</Include>
  </PropertyGroup>
```

`dotnet test` fails under 80% line coverage of Shared, Domain and Application (the hosts and Infrastructure are proved by the integration tests and conformance). The report goes under `bin/`, so nothing new appears in the tree. Run a filtered subset with `-p:CollectCoverage=false`, or the gate fails it.

## Files this option changes

| File | Change |
|---|---|
| `{Name}.slnx`, `Directory.Packages.props` | the integration project and its packages (`integration`, `architecture`); `coverlet.msbuild` (`architecture`) |
| `test/{Name}.Tests/{Name}.Tests.csproj` | the coverage properties and `coverlet.msbuild` (`architecture`) |
| `docker-compose.yml` | nothing: the integration tests start their own containers |

## Namespaces and usings

| File | Namespace | Usings |
|---|---|---|
| `{Command}ValidatorTests.cs` | `{Name}.Tests.Application.{Plural}` | `{Name}.Application.{Plural}` |
| `ScenarioHost.cs` | `{Name}.Tests.TestSupport.Scenarios` | `{Name}.Application`, `{Name}.Domain`, `{Name}.Shared`, `{Name}.Shared.Application`, `{Name}.Tests.TestSupport.Shared.Persistence`, `Microsoft.Extensions.DependencyInjection`, `Moq` |
| `CreateProductScenarioTests.cs` | `{Name}.Tests.Scenarios` | `{Name}.Application.Products`, `{Name}.Domain.Aggregates.Products`, `{Name}.Shared.Application`, `{Name}.Shared.Domain`, `{Name}.Shared.Exceptions`, `{Name}.Tests.TestSupport.Domain.Products`, `{Name}.Tests.TestSupport.Scenarios`, `Moq` |
| `PostgresFixture.cs`, `RabbitMqFixture.cs` | `{Name}.IntegrationTests.TestSupport` | `{Name}.Infrastructure.Persistence`, `DotNet.Testcontainers.Builders`, `Microsoft.EntityFrameworkCore`, `Npgsql`, `Testcontainers.PostgreSql` (or `Testcontainers.RabbitMq`) |
| `IntegrationConstants.cs`, `PostgresCollection.cs`, `OutboxRows.cs` | `{Name}.IntegrationTests.TestSupport` | none |
| `IntegrationServices.cs` | `{Name}.IntegrationTests.TestSupport` | `System.Linq.Expressions`, `{Name}.Application`, `{Name}.Domain`, `{Name}.Infrastructure`, `{Name}.Infrastructure.Persistence`, `{Name}.Shared`, `{Name}.Shared.Application`, `Microsoft.EntityFrameworkCore`, `Microsoft.Extensions.Configuration`, `Microsoft.Extensions.DependencyInjection` |
| `UnitOfWorkTests.cs` | `{Name}.IntegrationTests.Persistence` | `{Name}.Domain.Aggregates.Products`, `{Name}.IntegrationTests.TestSupport`, `{Name}.Shared.Persistence`, `Microsoft.Extensions.DependencyInjection` |
| `OutboxClaimTests.cs` | `{Name}.IntegrationTests.Messaging` | `{Name}.Infrastructure.Messaging.Outbox`, `{Name}.Infrastructure.Persistence`, `{Name}.IntegrationTests.TestSupport`, `Microsoft.EntityFrameworkCore`, `Microsoft.Extensions.DependencyInjection` |
| `ProductRepositoryTests.cs` (integration) | `{Name}.IntegrationTests.Persistence` | `{Name}.Application.Products`, `{Name}.Domain.Aggregates.Products`, `{Name}.IntegrationTests.TestSupport`, `{Name}.Shared.Exceptions` |
| `LayerRulesTests.cs` | `{Name}.Tests.Architecture` | `{Name}.Application`, `{Name}.Domain`, `{Name}.Shared` |
| `RegistrationRulesTests.cs` | `{Name}.Tests.Architecture` | `{Name}.Application`, `{Name}.Shared.Application`, `{Name}.Shared.Domain`, `Microsoft.Extensions.DependencyInjection` |
| `ServiceRulesTests.cs` | `{Name}.Tests.Architecture` | `System.Reflection`, `System.Runtime.CompilerServices`, `{Name}.Application`, `{Name}.Domain` |
| `EventContractTests.cs` | `{Name}.Tests.Architecture` | `{Name}.Tests.TestSupport.Architecture` |
| `EventContracts.cs` | `{Name}.Tests.TestSupport.Architecture` | `System.Reflection`, `System.Runtime.CompilerServices`, `System.Text.Json`, `System.Text.Json.Serialization`, `{Name}.Domain`, `{Name}.Infrastructure.Messaging.RabbitMq` (with RabbitMQ), `{Name}.Shared.Domain` |
| `RabbitMqRoundTripTests.cs` | `{Name}.IntegrationTests.Messaging` | `{Name}.Domain.Aggregates.Orders.Events`, `{Name}.Infrastructure.Messaging.RabbitMq`, `{Name}.IntegrationTests.TestSupport`, `Microsoft.Extensions.DependencyInjection`, `RabbitMQ.Client` |
| `RabbitMqDeliveries.cs`, `RabbitMqCollection.cs` | `{Name}.IntegrationTests.TestSupport` | `RabbitMQ.Client` (deliveries only) |

## Decision record

`docs/adr/0023-testing-levels.md`, in `project-docs-adr.md`.
