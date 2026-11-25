using Acme.Api.Middlewares;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Acme.Tests.Api.Middlewares;

public class CorrelationIdMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_WithAValidHeader_Should_UseItForTheContextAndTheResponse()
    {
        var correlationId = Guid.CreateVersion7();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[ApiConstants.CorrelationIdHeader] = correlationId.ToString();

        await new CorrelationIdMiddleware(_ => Task.CompletedTask, NullLogger<CorrelationIdMiddleware>.Instance).InvokeAsync(httpContext);

        Assert.Equal(correlationId, httpContext.Items[ApiConstants.CorrelationIdItem]);
        Assert.Equal(correlationId.ToString(), httpContext.Response.Headers[ApiConstants.CorrelationIdHeader].ToString());
    }

    [Fact]
    public async Task InvokeAsync_WithAMalformedHeader_Should_MakeANewIdInsteadOfFailing()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[ApiConstants.CorrelationIdHeader] = "not-a-guid";

        await new CorrelationIdMiddleware(_ => Task.CompletedTask, NullLogger<CorrelationIdMiddleware>.Instance).InvokeAsync(httpContext);

        var correlationId = Assert.IsType<Guid>(httpContext.Items[ApiConstants.CorrelationIdItem]);
        Assert.NotEqual(Guid.Empty, correlationId);
        Assert.Equal(correlationId.ToString(), httpContext.Response.Headers[ApiConstants.CorrelationIdHeader].ToString());
    }

    [Fact]
    public async Task InvokeAsync_Should_RunTheRestOfTheRequest()
    {
        var ran = false;

        await new CorrelationIdMiddleware(_ =>
        {
            ran = true;
            return Task.CompletedTask;
        }, NullLogger<CorrelationIdMiddleware>.Instance).InvokeAsync(new DefaultHttpContext());

        Assert.True(ran);
    }
}
