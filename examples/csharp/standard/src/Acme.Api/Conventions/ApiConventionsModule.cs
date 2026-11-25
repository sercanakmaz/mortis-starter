using System.Text.Json.Serialization;
using Acme.Api.Middlewares;
using Microsoft.AspNetCore.Mvc;

namespace Acme.Api.Conventions;

public static class ApiConventionsModule
{
    public static IMvcBuilder AddApiConventions(this IMvcBuilder mvcBuilder)
    {
        mvcBuilder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        return mvcBuilder
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            })
            .ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = InvalidRequest);
    }

    public static WebApplication UseApiConventions(this WebApplication app)
    {
        app.UseStatusCodePages(async statusCodeContext =>
        {
            var response = statusCodeContext.HttpContext.Response;

            if (response.StatusCode is not (StatusCodes.Status404NotFound or StatusCodes.Status405MethodNotAllowed))
            {
                return;
            }

            await response.WriteAsJsonAsync(response.StatusCode == StatusCodes.Status404NotFound
                ? new ErrorResponseModel(ApiConstants.RouteNotFoundCode, ApiConstants.RouteNotFoundMessage)
                : new ErrorResponseModel(ApiConstants.MethodNotAllowedCode, ApiConstants.MethodNotAllowedMessage));
        });

        return app;
    }

    private static IActionResult InvalidRequest(ActionContext actionContext)
    {
        var errors = actionContext.ModelState
            .Where(entry => entry.Value.Errors.Count > 0)
            .ToDictionary(entry => entry.Key.TrimStart('$', '.'), entry => entry.Value.Errors.Select(error => error.ErrorMessage).ToArray());

        return new BadRequestObjectResult(new ErrorResponseModel(ApiConstants.InvalidRequestCode, ApiConstants.InvalidRequestMessage, errors));
    }
}
