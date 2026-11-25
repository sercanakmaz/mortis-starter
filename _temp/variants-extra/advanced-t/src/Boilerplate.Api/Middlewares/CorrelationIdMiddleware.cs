namespace Boilerplate.Api.Middlewares;

public class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
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
}
