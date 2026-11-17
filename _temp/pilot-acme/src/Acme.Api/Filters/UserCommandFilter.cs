using System.Security.Claims;
using Acme.Shared.Application;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Acme.Api.Filters;

public class UserCommandFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var userId = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userEmail = context.HttpContext.User.FindFirstValue(ClaimTypes.Email);

        foreach (var userCommand in context.ActionArguments.Values.OfType<IUserCommand>())
        {
            userCommand.UserId = userId;
            userCommand.UserEmail = userEmail;
        }

        await next();
    }
}
