using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Boilerplate.Api.Observability;

public static class HealthResponse
{
    public static Task WriteAsync(HttpContext httpContext, HealthReport healthReport)
    {
        return httpContext.Response.WriteAsJsonAsync(new
        {
            Status = healthReport.Status.ToString(),
            Checks = healthReport.Entries.ToDictionary(entry => entry.Key, entry => entry.Value.Status.ToString())
        });
    }
}
