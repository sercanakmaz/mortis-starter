# Idempotent commands, C#

Applies when `addons` contains `idempotent-commands` and `database` is `postgres` (with MongoDB the add-on is not offered yet: R14, a note). Projects `{Name}.Api` (folder `Idempotency/`) and `{Name}.Infrastructure` (folder `Persistence/Idempotency/`). Layer 1: `architecture/http-and-operations.md`, `options/addons.md`.

**Not built or run yet** (decision 48): hand-written snippets; no variant builds this add-on. Phase 2 compiles and proves this page. `docs/unverified.md` lists it.

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
| `src/{Name}.Infrastructure/Persistence/AppDbContext.cs` | applies `IdempotencyRecordConfiguration`; a migration adds `idempotency_records` |

## IdempotencyRecord

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
        builder.Property(record => record.RequestHash).HasMaxLength(64).IsRequired();
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
}
```

`IdempotencySettings.cs` holds `RetentionHours` (24) and `InProgressTimeoutSeconds` (60), bound from the `Idempotency` section.

## IdempotencyStore

File `src/{Name}.Infrastructure/Persistence/Idempotency/IdempotencyStore.cs`:

```csharp
public class IdempotencyStore(IServiceScopeFactory serviceScopeFactory, IOptions<IdempotencySettings> settings)
{
    public async Task<IdempotencyRecord> StartAsync(string scope, string key, string requestHash)
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

    public async Task FinishAsync(string scope, string key, int statusCode, string location, string responseBody)
    {
        using var serviceScope = serviceScopeFactory.CreateScope();
        var appDbContext = serviceScope.ServiceProvider.GetRequiredService<AppDbContext>();

        await appDbContext.Database.ExecuteSqlAsync($"update idempotency_records set status_code = {statusCode}, location = {location}, response_body = {responseBody} where scope = {scope} and key = {key}");
    }

    public async Task ForgetAsync(string scope, string key)
    {
        using var serviceScope = serviceScopeFactory.CreateScope();
        var appDbContext = serviceScope.ServiceProvider.GetRequiredService<AppDbContext>();

        await appDbContext.Database.ExecuteSqlAsync($"delete from idempotency_records where scope = {scope} and key = {key} and status_code is null");
    }

    public async Task<int> DeleteExpiredAsync()
    {
        using var serviceScope = serviceScopeFactory.CreateScope();
        var appDbContext = serviceScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var expiredBefore = DateTime.UtcNow.AddHours(-settings.Value.RetentionHours);

        return await appDbContext.Database.ExecuteSqlAsync($"delete from idempotency_records where started_at < {expiredBefore}");
    }
}
```

- `StartAsync` is one statement: it inserts, or takes over an in-progress record older than the timeout, or does nothing. One affected row means the request owns the key; otherwise the existing record answers.
- A racing twin sends the same insert at the same moment; the primary key makes one of them affect nothing, and that one reads an in-progress record: 409.

`IdempotencyCleanupService.cs` is a `BackgroundService` that calls `DeleteExpiredAsync` once an hour and logs the count; `IdempotencyModule.cs` (`AddIdempotency`) binds the settings and registers the store as a singleton and the service.

## IdempotencyFilter

File `src/{Name}.Api/Idempotency/IdempotencyFilter.cs`, shown with `auth` = `jwt` (with `auth` = `none` the scope is the empty string and `AuthSettings` is not injected):

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
        var requestHash = await HashAsync(request);
        var record = await idempotencyStore.StartAsync(scope, key, requestHash);

        if (record is not null)
        {
            context.Result = Answer(context.HttpContext, record, requestHash);
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

    private static async Task<string> HashAsync(HttpRequest request)
    {
        request.EnableBuffering();

        using var buffer = new MemoryStream();
        await request.Body.CopyToAsync(buffer);
        request.Body.Position = 0;

        return Convert.ToHexString(SHA256.HashData([.. Encoding.UTF8.GetBytes($"{request.Method} {request.Path}\n"), .. buffer.ToArray()]));
    }

    private static IActionResult Answer(HttpContext httpContext, IdempotencyRecord record, string requestHash)
    {
        if (record.RequestHash != requestHash)
        {
            return new ObjectResult(new ErrorResponseModel(IdempotencyConstants.KeyReusedCode, "This idempotency key was used for another request.")) { StatusCode = StatusCodes.Status422UnprocessableEntity };
        }

        if (record.StatusCode is not { } statusCode)
        {
            return new ObjectResult(new ErrorResponseModel(IdempotencyConstants.InProgressCode, "A request with this idempotency key is still running.")) { StatusCode = StatusCodes.Status409Conflict };
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

- The two refusals are written straight as `ErrorResponseModel`, not thrown: a resource filter that short-circuits never reaches the exception handler with a body of its own. With `exceptions` = `coded` or `catalogued`, add the two codes to the catalogue and translate them like any other (`failures.md`); the snippet shows the plain message.
- A resource filter runs before model binding, so it reads the raw body; an action filter would see a bound command whose generated id differs on every request.

## Tests

`test/{Name}.Tests/Api/Idempotency/IdempotencyFilterTests.cs`, with a mocked store (`IdempotencyStore`'s methods are made `virtual` for Moq, or a builder in `TestSupport`):

```csharp
    [Fact]
    public async Task OnResourceExecutionAsync_WithAFinishedRecord_Should_ReplayItWithoutRunningTheAction()
    {
        var context = ResourceExecutingContextBuilder.Create().WithPost("/orders", """{"orderNumber":"A-1"}""").WithHeader(IdempotencyConstants.Header, "k-1").Build();
        var idempotencyStore = IdempotencyStoreBuilder.Create().WithStartAsync(FakeIdempotencyRecord.Finished(context, statusCode: 201, location: "orders/1", body: """{"id":"1"}""")).Build();
        var ran = false;

        await new IdempotencyFilter(idempotencyStore, Options.Create(FakeAuthSettings.Create()), Options.Create(new JsonOptions())).OnResourceExecutionAsync(context, () => { ran = true; return Task.FromResult<ResourceExecutedContext>(null); });

        var contentResult = Assert.IsType<ContentResult>(context.Result);
        Assert.Equal((201, """{"id":"1"}""", "true", "orders/1"), (contentResult.StatusCode, contentResult.Content, context.HttpContext.Response.Headers[IdempotencyConstants.ReplayedHeader].ToString(), context.HttpContext.Response.Headers.Location.ToString()));
        Assert.False(ran);
    }
```

And, in the same style: another hash gives 422 `idempotency_key_reused`; an in-progress record gives 409; no header, or a `PUT`, runs the action and never touches the store; a 201 object result calls `FinishAsync` with its body; an exception calls `ForgetAsync`. `test/{Name}.Tests/Infrastructure/Persistence/Idempotency/IdempotencyStoreTests.cs` runs against PostgreSQL like the repository tests: two `StartAsync` calls with the same key return null then the in-progress record; one older than the timeout is taken over; `DeleteExpiredAsync` removes only expired records. `scripts/conformance/idempotency.sh` proves it over HTTP.

## Namespaces and usings

| File | Namespace | Usings |
|---|---|---|
| `IdempotencyRecord.cs`, `IdempotencyConstants.cs`, `IdempotencySettings.cs` | `{Name}.Infrastructure.Persistence.Idempotency` | none |
| `IdempotencyRecordConfiguration.cs` | `{Name}.Infrastructure.Persistence.Idempotency` | `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Metadata.Builders` |
| `IdempotencyStore.cs`, `IdempotencyCleanupService.cs`, `IdempotencyModule.cs` | `{Name}.Infrastructure.Persistence.Idempotency` | `Microsoft.EntityFrameworkCore`, `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.Logging`, `Microsoft.Extensions.Options` |
| `IdempotencyFilter.cs` | `{Name}.Api.Idempotency` | `{Name}.Api.Middlewares`, `{Name}.Infrastructure.Authentication`, `{Name}.Infrastructure.Persistence.Idempotency`, `{Name}.Shared.Exceptions`, `Microsoft.AspNetCore.Mvc`, `Microsoft.AspNetCore.Mvc.Filters`, `Microsoft.Extensions.Options`, `System.Security.Cryptography`, `System.Text`, `System.Text.Json` |

## Decision record

`docs/adr/0021-idempotency-keys-for-post.md`, in `project-docs-adr.md`.
