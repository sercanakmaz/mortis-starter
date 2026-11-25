# The correlation add-on, C#

Applies when `addons` contains `correlation`. Layer 1: `events.md` "Correlation and causation", `options/addons.md`.

Without the add-on, every command still carries a correlation id and every event its correlation and causation ids (`shared-application.md`), and the log lines of commands and published events show them. The add-on connects them to the outside: a caller may send the id in a header, gets it back in the response, and every log line of the request is in a scope that carries it.

## The middleware

File `src/{Name}.Api/Middlewares/CorrelationIdMiddleware.cs`:

@@decl src/Boilerplate.Api/Middlewares/CorrelationIdMiddleware.cs::CorrelationIdMiddleware@@

@@member src/Boilerplate.Api/Middlewares/CorrelationIdMiddleware.cs::CorrelationIdMiddleware::InvokeAsync@@

- A header that is not a `Guid` is ignored and a new id is made; it is never an error.
- The id reaches `ContextCommandFilter` through `HttpContext.Items`; without the middleware the filter makes a new id per request.

In `Program.cs` of the Api, one line after `app.UseExceptionHandler();`:

```csharp
app.UseMiddleware<CorrelationIdMiddleware>();
```

## Tests

File `test/{Name}.Tests/Api/Middlewares/CorrelationIdMiddlewareTests.cs`:

@@members test/Boilerplate.Tests/Api/Middlewares/CorrelationIdMiddlewareTests.cs::CorrelationIdMiddlewareTests@@

## Decision record

`docs/adr/0009-correlation-header.md`, in `project-docs-adr.md`.

## Namespaces and usings

@@usings correlation.md@@
