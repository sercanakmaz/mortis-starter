using Boilerplate.Shared.Application;
using Boilerplate.Api.Middlewares;
using Microsoft.AspNetCore.Mvc.Filters;
using Boilerplate.Shared.Domain;
using System.Security.Claims;

namespace Boilerplate.Api.Filters;

public class ContextCommandFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var commandContext = new CommandContext
        {
            User = new AuditUser(context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), context.HttpContext.User.FindFirstValue(ClaimTypes.Email)),
            CorrelationId = context.HttpContext.Items[ApiConstants.CorrelationIdItem] is Guid correlationId ? correlationId : Guid.CreateVersion7()
        };

        foreach (var contextCommand in context.ActionArguments.Values.OfType<IContextCommand>())
        {
            contextCommand.Context = commandContext;
        }

        await next();
    }
}
