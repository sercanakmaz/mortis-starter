using Boilerplate.Api.Conventions;
using Boilerplate.Api.Middlewares;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Boilerplate.Tests.Api.Conventions;

public class ApiConventionsModuleTests
{
    [Fact]
    public void InvalidModelStateResponseFactory_Should_AnswerTheErrorBodyKeyedByMember()
    {
        var services = new ServiceCollection();
        services.AddControllers().AddApiConventions();
        var apiBehaviorOptions = services.BuildServiceProvider().GetRequiredService<IOptions<ApiBehaviorOptions>>().Value;
        var actionContext = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor());
        actionContext.ModelState.AddModelError("$.nmae", "The JSON property 'nmae' could not be mapped.");

        var result = Assert.IsType<BadRequestObjectResult>(apiBehaviorOptions.InvalidModelStateResponseFactory(actionContext));

        var errorResponseModel = Assert.IsType<ErrorResponseModel>(result.Value);
        Assert.Equal(ApiConstants.InvalidRequestCode, errorResponseModel.Code);
        Assert.Contains("nmae", errorResponseModel.Errors.Keys);
    }

    [Fact]
    public void AddApiConventions_Should_RefuseUnknownMembersAndWriteEnumsAsNames()
    {
        var services = new ServiceCollection();
        services.AddControllers().AddApiConventions();
        var jsonSerializerOptions = services.BuildServiceProvider().GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions;

        Assert.Equal(JsonUnmappedMemberHandling.Disallow, jsonSerializerOptions.UnmappedMemberHandling);
        Assert.Equal("\"Friday\"", JsonSerializer.Serialize(DayOfWeek.Friday, jsonSerializerOptions));
    }
}
