using Boilerplate.Infrastructure.Persistence.Idempotency;
using Microsoft.AspNetCore.Mvc;

namespace Boilerplate.Api.Idempotency;

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
