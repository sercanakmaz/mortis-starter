# Testing levels, C#

Applies to the `testing` key: `unit` (the core), `handlers`, `integration`, `architecture`; each level includes the ones before it. Layer 1: `options/testing.md`. The core's test project, rules and test support are on `testing.md` and `testing-kernel.md`.

**Not built or run yet** (decision 49): hand-written snippets; every variant is generated at `unit`. Phase 2 compiles and proves this page. `docs/unverified.md` lists it.

## What each level adds

| Level | Adds | Needs |
|---|---|---|
| `unit` | everything `testing.md` describes: domain, kernel, handler, host filter and mapping tests, all with fakes and builders, none touching I/O | nothing |
| `handlers` | a validator test per command; scenario tests that send a command through the real container, dispatcher, pipeline and validators, with only the persistence ports faked | nothing |
| `integration` | a second project `test/{Name}.IntegrationTests`: repositories, the unit of work, the outbox claim and (with RabbitMQ) the bus against a real PostgreSQL and broker, one fixture per collection, one database per test class | Docker, or `MORTIS_TEST_POSTGRES` / `MORTIS_TEST_RABBITMQ` |
| `architecture` | reflection rules (layer references, registrations, no private methods in a `*Service`), the event-contract golden file, a line-coverage gate | nothing |

- The core's handler tests stay at `unit`: they use no I/O, and every handler class has its test whatever the level. This differs from the plan's split (decision 49): the variants, which are the source of truth, ship handler tests in the lean preset.
- The house rules hold in the integration project: fixtures and builders in `TestSupport/`, test classes hold only tests. `testing.md`'s rule "no test talks to a database" is the unit project's rule; the integration project exists to break it.
- A test named on another page that needs PostgreSQL (`IdempotencyStoreTests`, `TraceLinkTests`) lives in the integration project and exists only at `integration` and above.
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

File `test/{Name}.Tests/TestSupport/Scenarios/ScenarioHost.cs`, the real composition with the persistence ports replaced:

```csharp
public sealed class ScenarioHost
{
    private readonly ServiceCollection _services = new();

    private ScenarioHost()
    {
        _services.AddLogging();
        _services.AddSharedModule().AddDomainModule().AddApplicationModule();
        _services.AddScoped(_ => UnitOfWorkBuilder.Create().WithHasTransaction(true).Build());
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

`test/{Name}.IntegrationTests/{Name}.IntegrationTests.csproj` references `{Name}.Infrastructure` and `{Name}.Application` and the packages `Testcontainers.PostgreSql`, `Testcontainers.RabbitMq` (with RabbitMQ), `Xunit.SkippableFact`, `Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`; it is added to `{Name}.slnx`, and its versions to `Directory.Packages.props`.

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

        if (!string.IsNullOrEmpty(ServerConnectionString))
        {
            return;
        }

        try
        {
            _container = new PostgreSqlBuilder().WithImage(IntegrationConstants.PostgresImage).Build();
            await _container.StartAsync();
            ServerConnectionString = _container.GetConnectionString();
        }
        catch (Exception exception) when (exception is DockerUnavailableException or HttpRequestException or TimeoutException)
        {
            Unavailable = $"No PostgreSQL: set {IntegrationConstants.PostgresVariable} or start Docker ({exception.GetType().Name}).";
        }
    }

    public async Task<string> CreateDatabaseAsync(string testClass)
    {
        var database = $"{IntegrationConstants.DatabasePrefix}{testClass.ToLowerInvariant()}_{Guid.CreateVersion7():N}";
        var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(new NpgsqlConnectionStringBuilder(ServerConnectionString) { Database = database }.ConnectionString).UseSnakeCaseNamingConvention().Options;

        await using var appDbContext = new AppDbContext(options);
        await appDbContext.Database.MigrateAsync();

        return appDbContext.Database.GetConnectionString();
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

The options repeat `InfrastructureModule`'s (Npgsql and, with `naming.database` = `snake_case`, the naming convention) so the migrated schema matches. With tenancy, `AppDbContext`'s constructor takes the `TenantScope` as in `tenancy.md`. `IntegrationConstants` holds `PostgresVariable = "MORTIS_TEST_POSTGRES"`, `RabbitMqVariable = "MORTIS_TEST_RABBITMQ"`, the images (`postgres:16`, `rabbitmq:4-management`) and `DatabasePrefix = "it_"`.

`IntegrationCollections.cs` declares `[CollectionDefinition(IntegrationConstants.PostgresCollection)] public class PostgresCollection : ICollectionFixture<PostgresFixture>;` and the same for RabbitMQ; one container serves the whole collection, and each test class makes its own database. `RabbitMqFixture.cs` is `PostgresFixture` for RabbitMQ (`RabbitMqBuilder`, `MORTIS_TEST_RABBITMQ`, an AMQP URI). `IntegrationServices.cs` builds the real container (`AddSharedModule` to `AddInfrastructureModule`) against a connection string, the way a host does.

A test, `test/{Name}.IntegrationTests/Persistence/ProductRepositoryTests.cs`:

```csharp
[Collection(IntegrationConstants.PostgresCollection)]
public class ProductRepositoryTests(PostgresFixture postgresFixture)
{
    [SkippableFact]
    public async Task Send_WithADuplicateSku_Should_ThrowDuplicateAndKeepOneRow()
    {
        Skip.If(postgresFixture.Unavailable is not null, postgresFixture.Unavailable);
        using var serviceProvider = IntegrationServices.Create(await postgresFixture.CreateDatabaseAsync(nameof(ProductRepositoryTests)));

        await serviceProvider.Send(FakeCreateProductCommand.WithSku("SKU-1"));
        await Assert.ThrowsAsync<DuplicateException>(() => serviceProvider.Send(FakeCreateProductCommand.WithSku("SKU-1")));

        Assert.Equal(1, await serviceProvider.CountAsync<Product>(product => product.Sku == "SKU-1"));
    }
}
```

The primary constructor is the xUnit collection-fixture injection, not a field or a helper; it is the one constructor a test class may have. A missing PostgreSQL makes every test **skipped** with the reason, never passed. The tests to write, each in its own class: a repository round trip per aggregate (create, find, update with the concurrency token, the duplicate and reference translations of `failures.md`); `UnitOfWorkTests` (a rolled-back command leaves no row and no outbox row); with an outbox, `OutboxClaimTests` (two concurrent claims get different rows, `for update skip locked`); with RabbitMQ, `RabbitMqRoundTripTests` (a published envelope reaches a bound consumer and a failing consumer's message reaches the error queue); with tenancy, a cross-tenant read returns nothing.

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

`test/{Name}.Tests/Architecture/RegistrationRulesTests.cs`, closing the gap ADR `0001` names (a missing registration otherwise fails only at the first `Send`):

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

Event consumers are registered by the hosts (`event-consumers.md`); the example's conformance proves theirs, and `verify.sh`'s structure check sees a consumer file.

`test/{Name}.Tests/Architecture/ServiceRulesTests.cs`: no type whose name ends in `Service` in Domain or Application declares a private method (`GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Where(method => method.IsPrivate && !method.IsSpecialName)`, compiler-generated members excluded by `CompilerGeneratedAttribute`).

`test/{Name}.Tests/Architecture/EventContractTests.cs` and the golden file `test/{Name}.Tests/Architecture/event-contracts.json` (copied to the output directory):

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

File `test/{Name}.Tests/TestSupport/Architecture/EventContracts.cs`: `Describe()` returns, for every event type that implements `IQueueEvent` (the events that leave the process), its wire name (`MessageNames` with RabbitMQ, the type's full name otherwise) and its JSON property names and types as the outbox serializes them, sorted, as one indented JSON string; `ReadPinned()` reads the golden file. With `MORTIS_UPDATE_CONTRACTS=1`, `ReadPinned` first writes `Describe()` over the golden file in the source tree: changing a contract is a deliberate, reviewed change to that file.

The coverage gate, in `{Name}.Tests.csproj`: the package `coverlet.msbuild` and

```xml
  <PropertyGroup>
    <CollectCoverage>true</CollectCoverage>
    <Threshold>80</Threshold>
    <ThresholdType>line</ThresholdType>
    <ExcludeByFile>**/Migrations/*.cs</ExcludeByFile>
  </PropertyGroup>
```

`dotnet test` fails under 80% line coverage of the source projects.

## Files this option changes

| File | Change |
|---|---|
| `{Name}.slnx`, `Directory.Packages.props` | the integration project and its packages (`integration`, `architecture`); `coverlet.msbuild` (`architecture`) |
| `test/{Name}.Tests/{Name}.Tests.csproj` | the coverage properties and the golden file as `Content` copied to the output (`architecture`) |
| `docker-compose.yml` | nothing: the integration tests start their own containers |

## Namespaces and usings

| File | Namespace | Usings |
|---|---|---|
| `{Command}ValidatorTests.cs` | `{Name}.Tests.Application.{Plural}` | `{Name}.Application.{Plural}` |
| `ScenarioHost.cs` | `{Name}.Tests.TestSupport.Scenarios` | `{Name}.Application`, `{Name}.Domain`, `{Name}.Shared`, `{Name}.Shared.Application`, `{Name}.Tests.TestSupport.Shared.Persistence`, `Microsoft.Extensions.DependencyInjection` |
| `CreateProductScenarioTests.cs` | `{Name}.Tests.Scenarios` | `{Name}.Application.Products`, `{Name}.Domain.Aggregates.Products`, `{Name}.Shared.Application`, `{Name}.Shared.Domain`, `{Name}.Shared.Exceptions`, `{Name}.Tests.TestSupport.Domain.Products`, `{Name}.Tests.TestSupport.Scenarios`, `Moq` |
| `PostgresFixture.cs`, `RabbitMqFixture.cs`, `IntegrationConstants.cs`, `IntegrationCollections.cs`, `IntegrationServices.cs` | `{Name}.IntegrationTests.TestSupport` | `{Name}.Infrastructure.Persistence`, `Microsoft.EntityFrameworkCore`, `Npgsql`, `Testcontainers.PostgreSql`, `Testcontainers.RabbitMq`, `DotNet.Testcontainers.Builders` |
| `ProductRepositoryTests.cs` (integration) | `{Name}.IntegrationTests.Persistence` | `{Name}.Domain.Aggregates.Products`, `{Name}.IntegrationTests.TestSupport`, `{Name}.Shared.Exceptions` |
| `LayerRulesTests.cs`, `RegistrationRulesTests.cs`, `ServiceRulesTests.cs`, `EventContractTests.cs` | `{Name}.Tests.Architecture` | `{Name}.Application`, `{Name}.Domain`, `{Name}.Shared`, `{Name}.Shared.Application`, `{Name}.Shared.Domain`, `{Name}.Tests.TestSupport.Architecture`, `Microsoft.Extensions.DependencyInjection`, `System.Reflection` |

## Decision record

`docs/adr/0023-testing-levels.md`, in `project-docs-adr.md`.
