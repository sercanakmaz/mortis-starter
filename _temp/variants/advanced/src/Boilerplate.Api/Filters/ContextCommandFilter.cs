using Boilerplate.Infrastructure.Authentication;
using Boilerplate.Infrastructure.Tenancy;
using Boilerplate.Shared.Application;
using Boilerplate.Api.Middlewares;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace Boilerplate.Api.Filters;

public class ContextCommandFilter(IOptions<AuthSettings> authSettings, TenantScope tenantScope) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var principal = context.HttpContext.User;
        var commandContext = new CommandContext
        {
            User = authSettings.Value.UserOf(principal),
            CorrelationId = context.HttpContext.Items[ApiConstants.CorrelationIdItem] is Guid correlationId ? correlationId : Guid.CreateVersion7(),
            TenantId = authSettings.Value.TenantOf(principal),
            Language = RequestLanguage.Of(context.HttpContext, authSettings.Value)
        };

        tenantScope.Enter(commandContext.TenantId);

        foreach (var contextCommand in context.ActionArguments.Values.OfType<IContextCommand>())
        {
            contextCommand.Context = commandContext;
        }

        await next();
    }
}
