# The correlation add-on, C#

Applies when `addons` contains `correlation`. Layer 1: `events.md` "Correlation and causation", `options/addons.md`.

Without the add-on, every command still carries a correlation id and every event its correlation and causation ids (`shared-application.md`), and the log lines of commands and published events show them. The add-on connects them to the outside: a caller may send the id in a header, gets it back in the response, and every log line of the request is in a scope that carries it.

## The middleware

File `src/{Name}.Api/Middlewares/CorrelationIdMiddleware.cs`:

```csharp
public class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
```

```csharp
public async Task InvokeAsync(HttpContext httpContext)
{
    var correlationId = Guid.TryParse(httpContext.Request.Headers[ApiConstants.CorrelationIdHeader], out var requestCorrelationId)
        ? requestCorrelationId
        : Guid.CreateVersion7();

    httpContext.Items[ApiConstants.CorrelationIdItem] = correlationId;
    httpContext.Response.Headers[ApiConstants.CorrelationIdHeader] = correlationId.ToString();

    using (logger.BeginScope(new Dictionary<string, object> { [ApiConstants.CorrelationIdItem] = correlationId }))
    {
        await next(httpContext);
    }
}
```

- A header that is not a `Guid` is ignored and a new id is made; it is never an error.
- The id reaches `ContextCommandFilter` through `HttpContext.Items`; without the middleware the filter makes a new id per request.

In `Program.cs` of the Api, one line after `app.UseExceptionHandler();`:

```csharp
app.UseMiddleware<CorrelationIdMiddleware>();
```

## Tests

File `test/{Name}.Tests/Api/Middlewares/CorrelationIdMiddlewareTests.cs`:

```csharp
public class CorrelationIdMiddlewareTests
```

```csharp
[Fact]
public async Task InvokeAsync_WithAValidHeader_Should_UseItForTheContextAndTheResponse()
{
    var correlationId = Guid.CreateVersion7();
    var httpContext = new DefaultHttpContext();
    httpContext.Request.Headers[ApiConstants.CorrelationIdHeader] = correlationId.ToString();

    await new CorrelationIdMiddleware(_ => Task.CompletedTask, NullLogger<CorrelationIdMiddleware>.Instance).InvokeAsync(httpContext);

    Assert.Equal(correlationId, httpContext.Items[ApiConstants.CorrelationIdItem]);
    Assert.Equal(correlationId.ToString(), httpContext.Response.Headers[ApiConstants.CorrelationIdHeader].ToString());
}
```

```csharp
[Fact]
public async Task InvokeAsync_WithAMalformedHeader_Should_MakeANewIdInsteadOfFailing()
{
    var httpContext = new DefaultHttpContext();
    httpContext.Request.Headers[ApiConstants.CorrelationIdHeader] = "not-a-guid";

    await new CorrelationIdMiddleware(_ => Task.CompletedTask, NullLogger<CorrelationIdMiddleware>.Instance).InvokeAsync(httpContext);

    var correlationId = Assert.IsType<Guid>(httpContext.Items[ApiConstants.CorrelationIdItem]);
    Assert.NotEqual(Guid.Empty, correlationId);
    Assert.Equal(correlationId.ToString(), httpContext.Response.Headers[ApiConstants.CorrelationIdHeader].ToString());
}
```

```csharp
[Fact]
public async Task InvokeAsync_Should_RunTheRestOfTheRequest()
{
    var ran = false;

    await new CorrelationIdMiddleware(_ =>
    {
        ran = true;
        return Task.CompletedTask;
    }, NullLogger<CorrelationIdMiddleware>.Instance).InvokeAsync(new DefaultHttpContext());

    Assert.True(ran);
}
```

## Decision record

`docs/adr/0009-correlation-header.md`, in `project-docs-adr.md`.

## Namespaces and usings

| File | Namespace | Usings |
|---|---|---|
| `src/{Name}.Api/Middlewares/CorrelationIdMiddleware.cs` | `{Name}.Api.Middlewares` | none |
| `test/{Name}.Tests/Api/Middlewares/CorrelationIdMiddlewareTests.cs` | `{Name}.Tests.Api.Middlewares` | Microsoft.AspNetCore.Http, Microsoft.Extensions.Logging.Abstractions, {Name}.Api.Middlewares |
