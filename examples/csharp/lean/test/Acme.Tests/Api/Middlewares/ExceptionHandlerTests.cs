using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Acme.Api.Middlewares;
using Acme.Shared.Exceptions;
using Acme.Tests.TestSupport.Infrastructure.Messaging;

namespace Acme.Tests.Api.Middlewares;

public class ExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_WithABusinessRule_Should_WriteItsCodeAndStatusAndLogAWarning()
    {
        var logger = new RecordingLogger<ExceptionHandler>();
        var httpContext = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

        await new ExceptionHandler(logger).TryHandleAsync(httpContext, new BusinessRuleException("order_not_open", "The order is not open."), CancellationToken.None);

        httpContext.Response.Body.Position = 0;
        var body = await JsonSerializer.DeserializeAsync<JsonElement>(httpContext.Response.Body);
        Assert.Equal(409, httpContext.Response.StatusCode);
        Assert.Equal("order_not_open", body.GetProperty("code").GetString());
        Assert.Equal(LogLevel.Warning, Assert.Single(logger.Entries).Level);
    }

    [Fact]
    public async Task TryHandleAsync_WithValidationFailed_Should_WriteTheFailingFields()
    {
        var httpContext = new DefaultHttpContext { Response = { Body = new MemoryStream() } };

        await new ExceptionHandler(new RecordingLogger<ExceptionHandler>()).TryHandleAsync(httpContext, new ValidationFailedException(new Dictionary<string, string[]> { ["Lines"] = ["required"] }), CancellationToken.None);

        httpContext.Response.Body.Position = 0;
        var body = await JsonSerializer.DeserializeAsync<JsonElement>(httpContext.Response.Body);
        Assert.Equal(422, httpContext.Response.StatusCode);
        Assert.Equal("required", body.GetProperty("errors").GetProperty("Lines")[0].GetString());
    }

    [Fact]
    public async Task TryHandleAsync_WithAnUnexpectedFailure_Should_Write500WithAReferenceAndLogAnError()
    {
        var logger = new RecordingLogger<ExceptionHandler>();
        var httpContext = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        var exception = new InvalidOperationException("secret detail");

        await new ExceptionHandler(logger).TryHandleAsync(httpContext, exception, CancellationToken.None);

        httpContext.Response.Body.Position = 0;
        var body = await JsonSerializer.DeserializeAsync<JsonElement>(httpContext.Response.Body);
        Assert.Equal(500, httpContext.Response.StatusCode);
        Assert.Equal(ApiConstants.UnexpectedErrorCode, body.GetProperty("code").GetString());
        Assert.Contains(ErrorReference.Of(exception), body.GetProperty("message").GetString());
        Assert.DoesNotContain("secret detail", body.GetProperty("message").GetString());
        Assert.Equal(LogLevel.Error, Assert.Single(logger.Entries).Level);
    }
}
