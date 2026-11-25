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
        var code = exceptionBase?.Code ?? ApiConstants.UnexpectedErrorCode;
        var reference = ErrorReference.Of(exception);
        var isServerError = httpStatusCode >= HttpStatusCode.InternalServerError;

        logger.Log(isServerError ? LogLevel.Error : LogLevel.Warning, isServerError ? exception : null, "Request failed with {StatusCode} {Code} reference {Reference}", (int)httpStatusCode, code, reference);

        httpContext.Response.StatusCode = (int)httpStatusCode;

        await httpContext.Response.WriteAsJsonAsync(new ErrorResponseModel(
            code,
            exceptionBase?.Message ?? $"{ApiConstants.UnexpectedErrorMessage} Reference {reference}.",
            (exception as ValidationFailedException)?.Errors), cancellationToken);

        return true;
    }
}
