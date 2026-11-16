using System.Diagnostics;

namespace Boilerplate.Api.Middlewares;

public class CorrelationIdMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext httpContext)
    {
        var correlationId = Guid.TryParse(httpContext.Request.Headers[ApiConstants.CorrelationIdHeader], out var requestCorrelationId)
            ? requestCorrelationId
            : Guid.CreateVersion7();

        Trace.CorrelationManager.ActivityId = correlationId;

        httpContext.Response.Headers[ApiConstants.CorrelationIdHeader] = correlationId.ToString();

        await next(httpContext);
    }
}
