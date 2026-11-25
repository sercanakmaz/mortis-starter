using System.Security.Claims;
using Acme.Api.Middlewares;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace Acme.Tests.TestSupport.Api;

public class ActionExecutingContextBuilder
{
    private readonly DefaultHttpContext _httpContext = new();
    private readonly Dictionary<string, object> _arguments = [];

    public static ActionExecutingContextBuilder Create() => new();

    public ActionExecutingContextBuilder WithUser(string id, string email)
    {
        _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id), new Claim(ClaimTypes.Email, email)], "test"));
        return this;
    }

    public ActionExecutingContextBuilder WithCorrelationId(Guid correlationId)
    {
        _httpContext.Items[ApiConstants.CorrelationIdItem] = correlationId;

        return this;
    }

    public ActionExecutingContextBuilder WithArgument(string name, object value)
    {
        _arguments[name] = value;
        return this;
    }

    public ActionExecutingContext Build()
    {
        var actionContext = new ActionContext(_httpContext, new RouteData(), new ActionDescriptor());

        return new ActionExecutingContext(actionContext, [], _arguments, new object());
    }

    public static ActionExecutionDelegate NextOf(ActionExecutingContext context) =>
        () => Task.FromResult(new ActionExecutedContext(context, [], new object()));
}
