using System.Text.Json;
using Boilerplate.Infrastructure.Authentication;
using Boilerplate.Infrastructure.Persistence.Idempotency;
using Boilerplate.Shared.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace Boilerplate.Api.Idempotency;

public class IdempotencyFilter(IdempotencyStore idempotencyStore, IOptions<AuthSettings> authSettings, IOptions<JsonOptions> jsonOptions) : IAsyncResourceFilter
{
    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        var key = request.Headers[IdempotencyConstants.Header].ToString();

        if (!HttpMethods.IsPost(request.Method) || string.IsNullOrWhiteSpace(key))
        {
            await next();
            return;
        }

        if (key.Length > IdempotencyConstants.KeyMaxLength)
        {
            throw new InvalidParameterException(IdempotencyConstants.Header, $"longer than {IdempotencyConstants.KeyMaxLength} characters");
        }

        var principal = context.HttpContext.User;
        var scope = $"{authSettings.Value.TenantOf(principal)}|{authSettings.Value.UserOf(principal)?.Id}";
        var requestHash = await IdempotencyHash.OfAsync(request);
        var record = await idempotencyStore.StartAsync(scope, key, requestHash);

        if (record is not null)
        {
            context.Result = IdempotencyReplay.Of(context.HttpContext, record, requestHash);
            return;
        }

        var executedContext = await next();

        if (executedContext.Exception is null && executedContext.Result is ObjectResult objectResult && (objectResult.StatusCode ?? StatusCodes.Status200OK) is >= 200 and < 300)
        {
            await idempotencyStore.FinishAsync(scope, key, objectResult.StatusCode ?? StatusCodes.Status200OK, context.HttpContext.Response.Headers.Location.ToString(), JsonSerializer.Serialize(objectResult.Value, jsonOptions.Value.JsonSerializerOptions));
        }
        else
        {
            await idempotencyStore.ForgetAsync(scope, key);
        }
    }
}
