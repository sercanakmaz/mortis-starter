using Boilerplate.Tests.TestSupport.Shared.Application;
using Boilerplate.Tests.TestSupport.Api;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Domain;
using Boilerplate.Tests.TestSupport.Infrastructure.Tenancy;
using Boilerplate.Infrastructure.Authentication;
using Microsoft.Extensions.Options;
using Boilerplate.Api.Filters;

namespace Boilerplate.Tests.Api.Filters;

public class ContextCommandFilterTests
{
    [Fact]
    public async Task OnActionExecutionAsync_WithAnAuthenticatedUser_Should_FillTheContextFromTheClaims()
    {
        var fakeContextCommand = new FakeContextCommand();
        var context = ActionExecutingContextBuilder.Create().WithUser("user-1", "user-1@localhost").WithArgument("command", fakeContextCommand).Build();

        await new ContextCommandFilter(Options.Create(new AuthSettings()), TenantScopeBuilder.Create().Build()).OnActionExecutionAsync(context, ActionExecutingContextBuilder.NextOf(context));

        Assert.Equal(["user-1", "user-1@localhost"], [fakeContextCommand.Context.User.Id, fakeContextCommand.Context.User.Email]);
    }

    [Fact]
    public async Task OnActionExecutionAsync_Should_TakeTheCorrelationIdOfTheRequest()
    {
        var correlationId = Guid.CreateVersion7();
        var fakeContextCommand = new FakeContextCommand();
        var context = ActionExecutingContextBuilder.Create().WithCorrelationId(correlationId).WithArgument("command", fakeContextCommand).Build();

        await new ContextCommandFilter(Options.Create(new AuthSettings()), TenantScopeBuilder.Create().Build()).OnActionExecutionAsync(context, ActionExecutingContextBuilder.NextOf(context));

        Assert.Equal(correlationId, fakeContextCommand.Context.CorrelationId);
    }

    [Fact]
    public async Task OnActionExecutionAsync_WithoutAUser_Should_ReplaceWhatTheClientSent()
    {
        var fakeContextCommand = new FakeContextCommand { Context = new CommandContext { User = new AuditUser("spoofed", "spoofed@localhost") } };
        var context = ActionExecutingContextBuilder.Create().WithArgument("command", fakeContextCommand).Build();

        await new ContextCommandFilter(Options.Create(new AuthSettings()), TenantScopeBuilder.Create().Build()).OnActionExecutionAsync(context, ActionExecutingContextBuilder.NextOf(context));

        Assert.Equal([null, null], [fakeContextCommand.Context.User.Id, fakeContextCommand.Context.User.Email]);
    }

    [Fact]
    public async Task OnActionExecutionAsync_Should_LeaveOtherArgumentsUntouchedAndRunTheAction()
    {
        var context = ActionExecutingContextBuilder.Create().WithUser("user-1", "user-1@localhost").WithArgument("id", Guid.Empty).WithArgument("command", new FakeCommand()).Build();
        var ran = false;

        await new ContextCommandFilter(Options.Create(new AuthSettings()), TenantScopeBuilder.Create().Build()).OnActionExecutionAsync(context, () =>
        {
            ran = true;
            return ActionExecutingContextBuilder.NextOf(context)();
        });

        Assert.True(ran);
        Assert.Equal(Guid.Empty, context.ActionArguments["id"]);
    }

    [Fact]
    public async Task OnActionExecutionAsync_WithATenantClaim_Should_PutItOnTheContextAndEnterTheTenantScope()
    {
        var tenantId = Guid.CreateVersion7();
        var tenantScope = TenantScopeBuilder.Create().Build();
        var fakeContextCommand = new FakeContextCommand();
        var context = ActionExecutingContextBuilder.Create().WithUser("user-1", "user-1@localhost").WithTenant(tenantId.ToString()).WithArgument("command", fakeContextCommand).Build();

        await new ContextCommandFilter(Options.Create(new AuthSettings()), tenantScope).OnActionExecutionAsync(context, ActionExecutingContextBuilder.NextOf(context));

        Assert.Equal(tenantId.ToString(), fakeContextCommand.Context.TenantId);
        Assert.Equal(tenantId, tenantScope.TenantId);
    }

    [Fact]
    public async Task OnActionExecutionAsync_Should_ReadTheClaimNamesFromTheSettings()
    {
        var fakeContextCommand = new FakeContextCommand();
        var context = ActionExecutingContextBuilder.Create().WithUser("user-1", "user-1@localhost").WithArgument("command", fakeContextCommand).Build();

        await new ContextCommandFilter(Options.Create(new AuthSettings { SubjectClaim = "email", EmailClaim = "sub" }), TenantScopeBuilder.Create().Build()).OnActionExecutionAsync(context, ActionExecutingContextBuilder.NextOf(context));

        Assert.Equal(["user-1@localhost", "user-1"], [fakeContextCommand.Context.User.Id, fakeContextCommand.Context.User.Email]);
    }
}
