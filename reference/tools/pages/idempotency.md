# Idempotent commands, C#

Applies when `addons` contains `idempotent-commands` and `database` is `postgres` (with MongoDB the add-on is not offered yet: R14, a note). Projects `{Name}.Api` (folder `Idempotency/`) and `{Name}.Infrastructure` (folder `Persistence/Idempotency/`). Layer 1: `architecture/http-and-operations.md`, `options/addons.md`.

**Built** in the advanced reference solution (decision 52): compiled, tested and verified there.

## How it fits together

```
POST + Idempotency-Key   IdempotencyFilter (an MVC resource filter: before binding, around the action and its result)
  hash  := SHA-256 of method, path and the raw body (the body is buffered and rewound)
  scope := tenant | user of the caller (empty with auth = none)
  store.StartAsync(scope, key, hash):
    null (this request owns the key) -> run; a success (2xx object result) -> FinishAsync(status, location, body); anything else -> ForgetAsync
    a record with another hash       -> 422 idempotency_key_reused
    a record in progress             -> 409 idempotency_in_progress
    a finished record                -> replay status, Location and body, header Idempotency-Replayed: true
IdempotencyCleanupService            every hour: delete records started before now - RetentionHours
```

- Only successes are stored. A failed request frees its key, so the client's retry runs again and fails or succeeds on its own merits.
- The record is written in its own scope and connection, outside the command's transaction, so a rolled-back command never leaves a finished record. The cost is the window named in layer 1: a crash after the commit and before `FinishAsync` leaves an in-progress record that a retry takes over after `InProgressTimeoutSeconds`, running the command again.
- The raw SQL uses the snake_case names; with `naming.database` = `as-code` the column names in the snippets follow the property names.

## Files this add-on changes

| File | Change |
|---|---|
| `src/{Name}.Api/Program.cs` | `options.Filters.Add<IdempotencyFilter>();` in `AddControllers`, and `builder.Services.AddIdempotency(builder.Configuration);` |
| `src/{Name}.Api/appsettings.json` | `"Idempotency": { "RetentionHours": 24, "InProgressTimeoutSeconds": 60 }` |
| `src/{Name}.Infrastructure/Persistence/AppDbContext.cs` | nothing: `ApplyConfigurationsFromAssembly` picks up `IdempotencyRecordConfiguration`; regenerate the `Initial` migration (`infrastructure-postgres.md`) to add `idempotency_records` |
| `src/{Name}.Domain/ErrorCatalogue.cs`, `messages.<language>.json` (catalogued, translated) | the codes `idempotency_key_reused` and `idempotency_in_progress` and their texts |

## The files

Shown with `auth` = `jwt` and tenancy (with `auth` = `none` the scope is the empty string and `AuthSettings` is not injected). `IdempotencyRecord.Of` exists for the tests; the store's methods are `virtual` so Moq can stand in for it.

File `src/{Name}.Infrastructure/Persistence/Idempotency/IdempotencyRecord.cs`:

```csharp
public class IdempotencyRecord
{
    public string Scope { get; private set; }
    public string Key { get; private set; }
    public string RequestHash { get; private set; }
    public DateTime StartedAt { get; private set; }
    public int? StatusCode { get; private set; }
    public string Location { get; private set; }
    public string ResponseBody { get; private set; }

    public static IdempotencyRecord Of(string scope, string key, string requestHash, int? statusCode = null, string location = null, string responseBody = null)
    {
        return new IdempotencyRecord { Scope = scope, Key = key, RequestHash = requestHash, StartedAt = DateTime.UtcNow, StatusCode = statusCode, Location = location, ResponseBody = responseBody };
    }
}
```

File `src/{Name}.Infrastructure/Persistence/Idempotency/IdempotencyRecordConfiguration.cs`:

```csharp
public class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable(IdempotencyConstants.Table);
        builder.HasKey(record => new { record.Scope, record.Key });
        builder.Property(record => record.Scope).HasMaxLength(IdempotencyConstants.ScopeMaxLength);
        builder.Property(record => record.Key).HasMaxLength(IdempotencyConstants.KeyMaxLength);
        builder.Property(record => record.RequestHash).HasMaxLength(IdempotencyConstants.HashLength).IsRequired();
        builder.HasIndex(record => record.StartedAt);
    }
}
```

File `src/{Name}.Infrastructure/Persistence/Idempotency/IdempotencyConstants.cs`:

```csharp
public static class IdempotencyConstants
{
    public const string Table = "idempotency_records";
    public const string Header = "Idempotency-Key";
    public const string ReplayedHeader = "Idempotency-Replayed";
    public const string KeyReusedCode = "idempotency_key_reused";
    public const string InProgressCode = "idempotency_in_progress";
    public const int KeyMaxLength = 200;
    public const int ScopeMaxLength = 300;
    public const int HashLength = 64;
    public const int CleanupIntervalMinutes = 60;
}
```

File `src/{Name}.Infrastructure/Persistence/Idempotency/IdempotencySettings.cs`:

```csharp
public class IdempotencySettings
{
    public const string Section = "Idempotency";

    public int RetentionHours { get; set; } = 24;
    public int InProgressTimeoutSeconds { get; set; } = 60;
}
```

File `src/{Name}.Infrastructure/Persistence/Idempotency/IdempotencyStore.cs`:

```csharp
public class IdempotencyStore(IServiceScopeFactory serviceScopeFactory, IOptions<IdempotencySettings> settings)
{
    public virtual async Task<IdempotencyRecord> StartAsync(string scope, string key, string requestHash)
    {
        using var serviceScope = serviceScopeFactory.CreateScope();
        var appDbContext = serviceScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;
        var takeOverBefore = now.AddSeconds(-settings.Value.InProgressTimeoutSeconds);

        var started = await appDbContext.Database.ExecuteSqlAsync($"""
            insert into idempotency_records (scope, key, request_hash, started_at) values ({scope}, {key}, {requestHash}, {now})
            on conflict (scope, key) do update set request_hash = excluded.request_hash, started_at = excluded.started_at
            where idempotency_records.status_code is null and idempotency_records.started_at < {takeOverBefore}
            """);

        return started == 1
            ? null
            : await appDbContext.Set<IdempotencyRecord>().AsNoTracking().SingleAsync(record => record.Scope == scope && record.Key == key);
    }

    public virtual async Task FinishAsync(string scope, string key, int statusCode, string location, string responseBody)
    {
        using var serviceScope = serviceScopeFactory.CreateScope();
        var appDbContext = serviceScope.ServiceProvider.GetRequiredService<AppDbContext>();

        await appDbContext.Database.ExecuteSqlAsync($"update idempotency_records set status_code = {statusCode}, location = {location}, response_body = {responseBody} where scope = {scope} and key = {key}");
    }

    public virtual async Task ForgetAsync(string scope, string key)
    {
        using var serviceScope = serviceScopeFactory.CreateScope();
        var appDbContext = serviceScope.ServiceProvider.GetRequiredService<AppDbContext>();

        await appDbContext.Database.ExecuteSqlAsync($"delete from idempotency_records where scope = {scope} and key = {key} and status_code is null");
    }

    public virtual async Task<int> DeleteExpiredAsync()
    {
        using var serviceScope = serviceScopeFactory.CreateScope();
        var appDbContext = serviceScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var expiredBefore = DateTime.UtcNow.AddHours(-settings.Value.RetentionHours);

        return await appDbContext.Database.ExecuteSqlAsync($"delete from idempotency_records where started_at < {expiredBefore}");
    }
}
```

File `src/{Name}.Infrastructure/Persistence/Idempotency/IdempotencyCleanupService.cs`:

```csharp
public class IdempotencyCleanupService(IdempotencyStore idempotencyStore, ILogger<IdempotencyCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var deleted = await idempotencyStore.DeleteExpiredAsync();

                logger.LogInformation("Deleted {Count} expired idempotency records", deleted);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(exception, "Deleting expired idempotency records failed; it tries again after the interval");
            }

            await Task.Delay(TimeSpan.FromMinutes(IdempotencyConstants.CleanupIntervalMinutes), stoppingToken);
        }
    }
}
```

File `src/{Name}.Infrastructure/Persistence/Idempotency/IdempotencyModule.cs`:

```csharp
public static class IdempotencyModule
{
    public static IServiceCollection AddIdempotency(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<IdempotencySettings>(configuration.GetSection(IdempotencySettings.Section));
        services.AddSingleton<IdempotencyStore>();
        services.AddHostedService<IdempotencyCleanupService>();

        return services;
    }
}
```

File `src/{Name}.Api/Idempotency/IdempotencyFilter.cs`:

```csharp
public class IdempotencyFilter(IdempotencyStore idempotencyStore, IOptions<AuthSettings> authSettings, IOptions<JsonOptions> jsonOptions) : IAsyncResourceFilter
{
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        var key = request.Headers[IdempotencyConstants.Header].ToString();

        if (!HttpMethods.IsPost(request.Method) || string.IsNullOrWhiteSpace(key))
        {
            await next();
            return;
        }

        if (key.Length > IdempotencyConstants.KeyMaxLength)
        {
            throw new InvalidParameterException(IdempotencyConstants.Header, $"longer than {IdempotencyConstants.KeyMaxLength} characters");
        }

        var principal = context.HttpContext.User;
        var scope = $"{authSettings.Value.TenantOf(principal)}|{authSettings.Value.UserOf(principal)?.Id}";
        var requestHash = await IdempotencyHash.OfAsync(request);
        var record = await idempotencyStore.StartAsync(scope, key, requestHash);

        if (record is not null)
        {
            context.Result = IdempotencyReplay.Of(context.HttpContext, record, requestHash);
            return;
        }

        var executedContext = await next();

        if (executedContext.Exception is null && executedContext.Result is ObjectResult objectResult && (objectResult.StatusCode ?? StatusCodes.Status200OK) is >= 200 and < 300)
        {
            await idempotencyStore.FinishAsync(scope, key, objectResult.StatusCode ?? StatusCodes.Status200OK, context.HttpContext.Response.Headers.Location.ToString(), JsonSerializer.Serialize(objectResult.Value, jsonOptions.Value.JsonSerializerOptions));
        }
        else
        {
            await idempotencyStore.ForgetAsync(scope, key);
        }
    }
}
```

File `src/{Name}.Api/Idempotency/IdempotencyHash.cs`:

```csharp
public static class IdempotencyHash
{
    public static async Task<string> OfAsync(HttpRequest request)
    {
        request.EnableBuffering();

        using var buffer = new MemoryStream();
        await request.Body.CopyToAsync(buffer);
        request.Body.Position = 0;

        return Convert.ToHexString(SHA256.HashData([.. Encoding.UTF8.GetBytes($"{request.Method} {request.Path}\n"), .. buffer.ToArray()]));
    }
}
```

File `src/{Name}.Api/Idempotency/IdempotencyReplay.cs`:

```csharp
public static class IdempotencyReplay
{
    public static IActionResult Of(HttpContext httpContext, IdempotencyRecord record, string requestHash)
    {
        if (record.RequestHash != requestHash)
        {
            throw new IdempotencyKeyReusedException();
        }

        if (record.StatusCode is not { } statusCode)
        {
            throw new IdempotencyInProgressException();
        }

        httpContext.Response.Headers[IdempotencyConstants.ReplayedHeader] = "true";

        if (!string.IsNullOrEmpty(record.Location))
        {
            httpContext.Response.Headers.Location = record.Location;
        }

        return new ContentResult { StatusCode = statusCode, Content = record.ResponseBody, ContentType = "application/json" };
    }
}
```

File `src/{Name}.Api/Idempotency/IdempotencyKeyReusedException.cs`:

```csharp
public class IdempotencyKeyReusedException() : ExceptionBase(IdempotencyConstants.KeyReusedCode, HttpStatusCode.UnprocessableEntity, "This idempotency key was used for another request.");
```

File `src/{Name}.Api/Idempotency/IdempotencyInProgressException.cs`:

```csharp
public class IdempotencyInProgressException() : ExceptionBase(IdempotencyConstants.InProgressCode, HttpStatusCode.Conflict, "A request with this idempotency key is still running.");
```

- `StartAsync` is one statement: it inserts, or takes over an in-progress record older than the timeout, or does nothing. One affected row means the request owns the key; otherwise the existing record answers. A racing twin's insert affects nothing and reads an in-progress record: 409.
- The two refusals are exceptions like any other failure: thrown from a resource filter they reach the exception handler, which writes the usual error body in the caller's language. With `exceptions` = `minimal` they still answer 422 and 409.
- The response body is stored as text, not `jsonb`: `jsonb` reorders and reformats it, and a replay must return the same bytes.
- A resource filter runs before model binding, so it reads the raw body; an action filter would see a bound command whose generated id differs on every request. It also wraps the result, so the `Location` header is set when the filter stores it.

## Tests

`test/{Name}.Tests/TestSupport/Api/Idempotency/ResourceExecutingContextBuilder.cs`:

```csharp
public class ResourceExecutingContextBuilder
{
    public const string OrderBody = """{"orderNumber":"A-1"}""";

    private readonly DefaultHttpContext _httpContext = new();

    public static ResourceExecutingContextBuilder Create() => new();

    public ResourceExecutingContextBuilder WithRequest(string method, string path, string body)
    {
        _httpContext.Request.Method = method;
        _httpContext.Request.Path = path;
        _httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));

        return this;
    }

    public ResourceExecutingContextBuilder WithHeader(string name, string value)
    {
        _httpContext.Request.Headers[name] = value;

        return this;
    }

    public ResourceExecutingContext Build()
    {
        return new ResourceExecutingContext(new ActionContext(_httpContext, new RouteData(), new ActionDescriptor()), [], []);
    }

    public static ResourceExecutionDelegate NextOf(ResourceExecutingContext context, IActionResult result, List<string> ran)
    {
        return () =>
        {
            ran.Add("action");

            return Task.FromResult(new ResourceExecutedContext(context, []) { Result = result });
        };
    }
}
```

`test/{Name}.Tests/TestSupport/Api/Idempotency/IdempotencyStoreBuilder.cs`:

```csharp
public class IdempotencyStoreBuilder
{
    private readonly Mock<IdempotencyStore> _mock = new(Mock.Of<IServiceScopeFactory>(), Options.Create(new IdempotencySettings()));

    public static IdempotencyStoreBuilder Create() => new();

    public List<string> Calls { get; } = [];

    public IdempotencyStoreBuilder WithStartAsync(IdempotencyRecord record)
    {
        _mock.Setup(store => store.StartAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).ReturnsAsync(record).Callback(() => Calls.Add(nameof(IdempotencyStore.StartAsync)));
        _mock.Setup(store => store.FinishAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask).Callback<string, string, int, string, string>((_, _, statusCode, _, body) => Calls.Add($"{nameof(IdempotencyStore.FinishAsync)} {statusCode} {body}"));
        _mock.Setup(store => store.ForgetAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask).Callback(() => Calls.Add(nameof(IdempotencyStore.ForgetAsync)));

        return this;
    }

    public IdempotencyStore Build() => _mock.Object;
}
```

`test/{Name}.Tests/Api/Idempotency/IdempotencyFilterTests.cs`:

```csharp
public class IdempotencyFilterTests
{
    [Fact]
    public async Task OnResourceExecutionAsync_WithAFinishedRecord_Should_ReplayItWithoutRunningTheAction()
    {
        var context = ResourceExecutingContextBuilder.Create().WithRequest(HttpMethods.Post, "/orders", ResourceExecutingContextBuilder.OrderBody).WithHeader(IdempotencyConstants.Header, "k-1").Build();
        var requestHash = await IdempotencyHash.OfAsync(context.HttpContext.Request);
        var idempotencyStoreBuilder = IdempotencyStoreBuilder.Create().WithStartAsync(IdempotencyRecord.Of("|", "k-1", requestHash, 201, "orders/1", """{"id":"1"}"""));
        var ran = new List<string>();

        await new IdempotencyFilter(idempotencyStoreBuilder.Build(), Options.Create(FakeAuthSettings.Create()), Options.Create(new JsonOptions())).OnResourceExecutionAsync(context, ResourceExecutingContextBuilder.NextOf(context, null, ran));

        var contentResult = Assert.IsType<ContentResult>(context.Result);
        Assert.Equal((201, """{"id":"1"}""", "true", "orders/1"), (contentResult.StatusCode, contentResult.Content, context.HttpContext.Response.Headers[IdempotencyConstants.ReplayedHeader].ToString(), context.HttpContext.Response.Headers.Location.ToString()));
        Assert.Empty(ran);
    }

    [Fact]
    public async Task OnResourceExecutionAsync_WithARecordOfAnotherRequest_Should_ThrowKeyReused()
    {
        var context = ResourceExecutingContextBuilder.Create().WithRequest(HttpMethods.Post, "/orders", ResourceExecutingContextBuilder.OrderBody).WithHeader(IdempotencyConstants.Header, "k-1").Build();
        var idempotencyStoreBuilder = IdempotencyStoreBuilder.Create().WithStartAsync(IdempotencyRecord.Of("|", "k-1", "another-hash", 201));

        var exception = await Assert.ThrowsAsync<IdempotencyKeyReusedException>(() => new IdempotencyFilter(idempotencyStoreBuilder.Build(), Options.Create(FakeAuthSettings.Create()), Options.Create(new JsonOptions())).OnResourceExecutionAsync(context, ResourceExecutingContextBuilder.NextOf(context, null, [])));

        Assert.Equal(IdempotencyConstants.KeyReusedCode, exception.Code);
    }

    [Fact]
    public async Task OnResourceExecutionAsync_WithARecordInProgress_Should_ThrowInProgress()
    {
        var context = ResourceExecutingContextBuilder.Create().WithRequest(HttpMethods.Post, "/orders", ResourceExecutingContextBuilder.OrderBody).WithHeader(IdempotencyConstants.Header, "k-1").Build();
        var requestHash = await IdempotencyHash.OfAsync(context.HttpContext.Request);
        var idempotencyStoreBuilder = IdempotencyStoreBuilder.Create().WithStartAsync(IdempotencyRecord.Of("|", "k-1", requestHash));

        await Assert.ThrowsAsync<IdempotencyInProgressException>(() => new IdempotencyFilter(idempotencyStoreBuilder.Build(), Options.Create(FakeAuthSettings.Create()), Options.Create(new JsonOptions())).OnResourceExecutionAsync(context, ResourceExecutingContextBuilder.NextOf(context, null, [])));
    }

    [Theory]
    [InlineData("POST", "")]
    [InlineData("PUT", "k-1")]
    public async Task OnResourceExecutionAsync_WithoutAKeyOrNotAPost_Should_RunTheActionAndNotTouchTheStore(string method, string key)
    {
        var context = ResourceExecutingContextBuilder.Create().WithRequest(method, "/orders", ResourceExecutingContextBuilder.OrderBody).WithHeader(IdempotencyConstants.Header, key).Build();
        var idempotencyStoreBuilder = IdempotencyStoreBuilder.Create().WithStartAsync(null);
        var ran = new List<string>();

        await new IdempotencyFilter(idempotencyStoreBuilder.Build(), Options.Create(FakeAuthSettings.Create()), Options.Create(new JsonOptions())).OnResourceExecutionAsync(context, ResourceExecutingContextBuilder.NextOf(context, new OkResult(), ran));

        Assert.Equal(["action"], ran);
        Assert.Empty(idempotencyStoreBuilder.Calls);
    }

    [Fact]
    public async Task OnResourceExecutionAsync_WithANewKeyAndASuccess_Should_FinishTheRecordWithTheBody()
    {
        var context = ResourceExecutingContextBuilder.Create().WithRequest(HttpMethods.Post, "/orders", ResourceExecutingContextBuilder.OrderBody).WithHeader(IdempotencyConstants.Header, "k-1").Build();
        var idempotencyStoreBuilder = IdempotencyStoreBuilder.Create().WithStartAsync(null);

        await new IdempotencyFilter(idempotencyStoreBuilder.Build(), Options.Create(FakeAuthSettings.Create()), Options.Create(new JsonOptions())).OnResourceExecutionAsync(context, ResourceExecutingContextBuilder.NextOf(context, new ObjectResult(new { Id = "1" }) { StatusCode = 201 }, []));

        Assert.Equal([nameof(IdempotencyStore.StartAsync), $"{nameof(IdempotencyStore.FinishAsync)} 201 {{\"id\":\"1\"}}"], idempotencyStoreBuilder.Calls);
    }

    [Fact]
    public async Task OnResourceExecutionAsync_WithANewKeyAndAFailure_Should_ForgetTheKey()
    {
        var context = ResourceExecutingContextBuilder.Create().WithRequest(HttpMethods.Post, "/orders", ResourceExecutingContextBuilder.OrderBody).WithHeader(IdempotencyConstants.Header, "k-1").Build();
        var idempotencyStoreBuilder = IdempotencyStoreBuilder.Create().WithStartAsync(null);

        await new IdempotencyFilter(idempotencyStoreBuilder.Build(), Options.Create(FakeAuthSettings.Create()), Options.Create(new JsonOptions())).OnResourceExecutionAsync(context, ResourceExecutingContextBuilder.NextOf(context, new BadRequestResult(), []));

        Assert.Equal([nameof(IdempotencyStore.StartAsync), nameof(IdempotencyStore.ForgetAsync)], idempotencyStoreBuilder.Calls);
    }
}
```

With an integration level, `test/{Name}.IntegrationTests/Persistence/IdempotencyStoreTests.cs` against PostgreSQL:

```csharp
[Collection(IntegrationConstants.PostgresCollection)]
public class IdempotencyStoreTests(PostgresFixture postgresFixture)
{
    [SkippableFact]
    public async Task StartAsync_TwiceWithTheSameKey_Should_OwnTheKeyThenReturnTheRecordInProgress()
    {
        Skip.If(postgresFixture.Unavailable is not null, postgresFixture.Unavailable);
        await using var serviceProvider = IntegrationServices.Create(await postgresFixture.CreateDatabaseAsync(nameof(IdempotencyStoreTests)));
        var idempotencyStore = new IdempotencyStore(serviceProvider.GetRequiredService<IServiceScopeFactory>(), Options.Create(new IdempotencySettings()));

        var first = await idempotencyStore.StartAsync("tenant|user", "k-1", "hash");
        var second = await idempotencyStore.StartAsync("tenant|user", "k-1", "hash");

        Assert.Null(first);
        Assert.Equal(("hash", null), (second.RequestHash, second.StatusCode));
    }

    [SkippableFact]
    public async Task StartAsync_WithARecordInProgressOlderThanTheTimeout_Should_TakeItOver()
    {
        Skip.If(postgresFixture.Unavailable is not null, postgresFixture.Unavailable);
        await using var serviceProvider = IntegrationServices.Create(await postgresFixture.CreateDatabaseAsync(nameof(IdempotencyStoreTests)));
        var idempotencyStore = new IdempotencyStore(serviceProvider.GetRequiredService<IServiceScopeFactory>(), Options.Create(new IdempotencySettings { InProgressTimeoutSeconds = -1 }));
        await idempotencyStore.StartAsync("tenant|user", "k-2", "hash");

        var takenOver = await idempotencyStore.StartAsync("tenant|user", "k-2", "hash");

        Assert.Null(takenOver);
    }

    [SkippableFact]
    public async Task FinishAsync_ThenDeleteExpiredAsync_Should_ReplayTheBodyAndDeleteOnlyExpiredRecords()
    {
        Skip.If(postgresFixture.Unavailable is not null, postgresFixture.Unavailable);
        await using var serviceProvider = IntegrationServices.Create(await postgresFixture.CreateDatabaseAsync(nameof(IdempotencyStoreTests)));
        var idempotencyStore = new IdempotencyStore(serviceProvider.GetRequiredService<IServiceScopeFactory>(), Options.Create(new IdempotencySettings()));
        await idempotencyStore.StartAsync("tenant|user", "k-3", "hash");
        await idempotencyStore.FinishAsync("tenant|user", "k-3", 201, "orders/1", """{"id":"1"}""");

        var deleted = await idempotencyStore.DeleteExpiredAsync();
        var finished = await idempotencyStore.StartAsync("tenant|user", "k-3", "hash");

        Assert.Equal(0, deleted);
        Assert.Equal((201, "orders/1"), (finished.StatusCode, finished.Location));
        Assert.Contains("\"id\"", finished.ResponseBody);
    }
}
```

`scripts/conformance/idempotency.sh` proves it over HTTP.

## Namespaces and usings

@@usings idempotency.md@@

## Decision record

`docs/adr/0021-idempotency-keys-for-post.md`, in `project-docs-adr.md`.
