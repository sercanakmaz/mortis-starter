using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace Boilerplate.Tests.TestSupport.Api.Idempotency;

public class ResourceExecutingContextBuilder
{
    public const string OrderBody = """{"orderNumber":"A-1"}""";

    private readonly DefaultHttpContext _httpContext = new();

    public static ResourceExecutingContextBuilder Create() => new();

    public ResourceExecutingContextBuilder WithRequest(string method, string path, string body)
    {
        _httpContext.Request.Method = method;
        _httpContext.Request.Path = path;
        _httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));

        return this;
    }

    public ResourceExecutingContextBuilder WithHeader(string name, string value)
    {
        _httpContext.Request.Headers[name] = value;

        return this;
    }

    public ResourceExecutingContext Build()
    {
        return new ResourceExecutingContext(new ActionContext(_httpContext, new RouteData(), new ActionDescriptor()), [], []);
    }

    public static ResourceExecutionDelegate NextOf(ResourceExecutingContext context, IActionResult result, List<string> ran)
    {
        return () =>
        {
            ran.Add("action");

            return Task.FromResult(new ResourceExecutedContext(context, []) { Result = result });
        };
    }
}
