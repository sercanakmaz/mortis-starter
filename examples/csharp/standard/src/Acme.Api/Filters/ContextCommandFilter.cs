using Acme.Api.Middlewares;
using Acme.Infrastructure.Authentication;
using Acme.Shared.Application;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace Acme.Api.Filters;

public class ContextCommandFilter(IOptions<AuthSettings> authSettings) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var principal = context.HttpContext.User;
        var commandContext = new CommandContext
        {
            User = authSettings.Value.UserOf(principal),
            CorrelationId = context.HttpContext.Items[ApiConstants.CorrelationIdItem] is Guid correlationId ? correlationId : Guid.CreateVersion7()
        };

        foreach (var contextCommand in context.ActionArguments.Values.OfType<IContextCommand>())
        {
            contextCommand.Context = commandContext;
        }

        foreach (var query in context.ActionArguments.Values.OfType<IQuery>())
        {
            query.Context = commandContext;
        }

        await next();
    }
}
