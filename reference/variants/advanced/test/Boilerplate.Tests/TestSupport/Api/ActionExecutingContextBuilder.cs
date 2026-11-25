using Boilerplate.Api.Middlewares;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using System.Security.Claims;

namespace Boilerplate.Tests.TestSupport.Api;

public class ActionExecutingContextBuilder
{
    private readonly DefaultHttpContext _httpContext = new();
    private readonly Dictionary<string, object> _arguments = [];

    public static ActionExecutingContextBuilder Create() => new();

    private readonly List<Claim> _claims = [];

    public ActionExecutingContextBuilder WithUser(string id, string email)
    {
        _claims.AddRange([new Claim("sub", id), new Claim("email", email)]);
        return this;
    }

    public ActionExecutingContextBuilder WithTenant(string tenantId)
    {
        _claims.Add(new Claim("tenant_id", tenantId));
        return this;
    }

    public ActionExecutingContextBuilder WithLanguageClaim(string language)
    {
        _claims.Add(new Claim("locale", language));
        return this;
    }

    public ActionExecutingContextBuilder WithAcceptLanguage(string acceptLanguage)
    {
        _httpContext.Request.Headers.AcceptLanguage = acceptLanguage;
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
        if (_claims.Count > 0) _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(_claims, "test"));

        var actionContext = new ActionContext(_httpContext, new RouteData(), new ActionDescriptor());

        return new ActionExecutingContext(actionContext, [], _arguments, new object());
    }

    public static ActionExecutionDelegate NextOf(ActionExecutingContext context) =>
        () => Task.FromResult(new ActionExecutedContext(context, [], new object()));
}
