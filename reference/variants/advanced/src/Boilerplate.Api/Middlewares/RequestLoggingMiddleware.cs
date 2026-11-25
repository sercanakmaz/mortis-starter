using Boilerplate.Infrastructure.Diagnostics;
using System.Diagnostics;

namespace Boilerplate.Api.Middlewares;

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
