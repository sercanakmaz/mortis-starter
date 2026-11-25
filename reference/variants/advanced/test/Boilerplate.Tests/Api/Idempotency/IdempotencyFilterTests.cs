using Boilerplate.Api.Idempotency;
using Boilerplate.Infrastructure.Persistence.Idempotency;
using Boilerplate.Tests.TestSupport.Api.Idempotency;
using Boilerplate.Tests.TestSupport.Infrastructure.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Boilerplate.Tests.Api.Idempotency;

public class IdempotencyFilterTests
{
    [Fact]
    public async Task OnResourceExecutionAsync_WithAFinishedRecord_Should_ReplayItWithoutRunningTheAction()
    {
        var context = ResourceExecutingContextBuilder.Create().WithRequest(HttpMethods.Post, "/orders", ResourceExecutingContextBuilder.OrderBody).WithHeader(IdempotencyConstants.Header, "k-1").Build();
        var requestHash = await IdempotencyHash.OfAsync(context.HttpContext.Request);
        var idempotencyStoreBuilder = IdempotencyStoreBuilder.Create().WithStartAsync(IdempotencyRecord.Of("|", "k-1", requestHash, 201, "orders/1", """{"id":"1"}"""));
        var ran = new List<string>();

        await new IdempotencyFilter(idempotencyStoreBuilder.Build(), Options.Create(FakeAuthSettings.Create()), Options.Create(new JsonOptions())).OnResourceExecutionAsync(context, ResourceExecutingContextBuilder.NextOf(context, null, ran));

        var contentResult = Assert.IsType<ContentResult>(context.Result);
        Assert.Equal((201, """{"id":"1"}""", "true", "orders/1"), (contentResult.StatusCode, contentResult.Content, context.HttpContext.Response.Headers[IdempotencyConstants.ReplayedHeader].ToString(), context.HttpContext.Response.Headers.Location.ToString()));
        Assert.Empty(ran);
    }

    [Fact]
    public async Task OnResourceExecutionAsync_WithARecordOfAnotherRequest_Should_ThrowKeyReused()
    {
        var context = ResourceExecutingContextBuilder.Create().WithRequest(HttpMethods.Post, "/orders", ResourceExecutingContextBuilder.OrderBody).WithHeader(IdempotencyConstants.Header, "k-1").Build();
        var idempotencyStoreBuilder = IdempotencyStoreBuilder.Create().WithStartAsync(IdempotencyRecord.Of("|", "k-1", "another-hash", 201));

        var exception = await Assert.ThrowsAsync<IdempotencyKeyReusedException>(() => new IdempotencyFilter(idempotencyStoreBuilder.Build(), Options.Create(FakeAuthSettings.Create()), Options.Create(new JsonOptions())).OnResourceExecutionAsync(context, ResourceExecutingContextBuilder.NextOf(context, null, [])));

        Assert.Equal(IdempotencyConstants.KeyReusedCode, exception.Code);
    }

    [Fact]
    public async Task OnResourceExecutionAsync_WithARecordInProgress_Should_ThrowInProgress()
    {
        var context = ResourceExecutingContextBuilder.Create().WithRequest(HttpMethods.Post, "/orders", ResourceExecutingContextBuilder.OrderBody).WithHeader(IdempotencyConstants.Header, "k-1").Build();
        var requestHash = await IdempotencyHash.OfAsync(context.HttpContext.Request);
        var idempotencyStoreBuilder = IdempotencyStoreBuilder.Create().WithStartAsync(IdempotencyRecord.Of("|", "k-1", requestHash));

        await Assert.ThrowsAsync<IdempotencyInProgressException>(() => new IdempotencyFilter(idempotencyStoreBuilder.Build(), Options.Create(FakeAuthSettings.Create()), Options.Create(new JsonOptions())).OnResourceExecutionAsync(context, ResourceExecutingContextBuilder.NextOf(context, null, [])));
    }

    [Theory]
    [InlineData("POST", "")]
    [InlineData("PUT", "k-1")]
    public async Task OnResourceExecutionAsync_WithoutAKeyOrNotAPost_Should_RunTheActionAndNotTouchTheStore(string method, string key)
    {
        var context = ResourceExecutingContextBuilder.Create().WithRequest(method, "/orders", ResourceExecutingContextBuilder.OrderBody).WithHeader(IdempotencyConstants.Header, key).Build();
        var idempotencyStoreBuilder = IdempotencyStoreBuilder.Create().WithStartAsync(null);
        var ran = new List<string>();

        await new IdempotencyFilter(idempotencyStoreBuilder.Build(), Options.Create(FakeAuthSettings.Create()), Options.Create(new JsonOptions())).OnResourceExecutionAsync(context, ResourceExecutingContextBuilder.NextOf(context, new OkResult(), ran));

        Assert.Equal(["action"], ran);
        Assert.Empty(idempotencyStoreBuilder.Calls);
    }

    [Fact]
    public async Task OnResourceExecutionAsync_WithANewKeyAndASuccess_Should_FinishTheRecordWithTheBody()
    {
        var context = ResourceExecutingContextBuilder.Create().WithRequest(HttpMethods.Post, "/orders", ResourceExecutingContextBuilder.OrderBody).WithHeader(IdempotencyConstants.Header, "k-1").Build();
        var idempotencyStoreBuilder = IdempotencyStoreBuilder.Create().WithStartAsync(null);

        await new IdempotencyFilter(idempotencyStoreBuilder.Build(), Options.Create(FakeAuthSettings.Create()), Options.Create(new JsonOptions())).OnResourceExecutionAsync(context, ResourceExecutingContextBuilder.NextOf(context, new ObjectResult(new { Id = "1" }) { StatusCode = 201 }, []));

        Assert.Equal([nameof(IdempotencyStore.StartAsync), $"{nameof(IdempotencyStore.FinishAsync)} 201 {{\"id\":\"1\"}}"], idempotencyStoreBuilder.Calls);
    }

    [Fact]
    public async Task OnResourceExecutionAsync_WithANewKeyAndAFailure_Should_ForgetTheKey()
    {
        var context = ResourceExecutingContextBuilder.Create().WithRequest(HttpMethods.Post, "/orders", ResourceExecutingContextBuilder.OrderBody).WithHeader(IdempotencyConstants.Header, "k-1").Build();
        var idempotencyStoreBuilder = IdempotencyStoreBuilder.Create().WithStartAsync(null);

        await new IdempotencyFilter(idempotencyStoreBuilder.Build(), Options.Create(FakeAuthSettings.Create()), Options.Create(new JsonOptions())).OnResourceExecutionAsync(context, ResourceExecutingContextBuilder.NextOf(context, new BadRequestResult(), []));

        Assert.Equal([nameof(IdempotencyStore.StartAsync), nameof(IdempotencyStore.ForgetAsync)], idempotencyStoreBuilder.Calls);
    }
}
