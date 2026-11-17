using System.Net;
using Acme.Shared.Exceptions;
using Microsoft.AspNetCore.Diagnostics;

namespace Acme.Api.Middlewares;

public class ExceptionHandler(ILogger<ExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var exceptionBase = exception as ExceptionBase;
        var httpStatusCode = exceptionBase?.HttpStatusCode ?? HttpStatusCode.InternalServerError;

        if (httpStatusCode >= HttpStatusCode.InternalServerError)
        {
            logger.LogError(exception, "Request failed with {StatusCode}", (int)httpStatusCode);
        }

        httpContext.Response.StatusCode = (int)httpStatusCode;

        await httpContext.Response.WriteAsJsonAsync(new ErrorResponseModel(
            exceptionBase?.Code ?? ApiConstants.UnexpectedErrorCode,
            exceptionBase?.Message ?? ApiConstants.UnexpectedErrorMessage,
            (exception as ValidationFailedException)?.Errors), cancellationToken);

        return true;
    }
}
