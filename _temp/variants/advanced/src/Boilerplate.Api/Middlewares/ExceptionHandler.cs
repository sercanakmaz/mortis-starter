using Boilerplate.Infrastructure.Authentication;
using Boilerplate.Infrastructure.Localization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Options;
using Boilerplate.Shared.Exceptions;
using System.Net;

namespace Boilerplate.Api.Middlewares;

public class ExceptionHandler(ILogger<ExceptionHandler> logger, MessageLocalizer messageLocalizer, IOptions<AuthSettings> authSettings) : IExceptionHandler
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
            exceptionBase is null ? $"{ApiConstants.UnexpectedErrorMessage} Reference {reference}." : messageLocalizer.Get(code, exceptionBase.Parameters, RequestLanguage.Of(httpContext, authSettings.Value)) ?? exceptionBase.Message,
            (exception as ValidationFailedException)?.Errors), cancellationToken);

        return true;
    }
}
