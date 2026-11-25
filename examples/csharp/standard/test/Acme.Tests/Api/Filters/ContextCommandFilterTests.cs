using Acme.Api.Filters;
using Acme.Shared.Application;
using Acme.Shared.Domain;
using Acme.Tests.TestSupport.Api;
using Acme.Tests.TestSupport.Infrastructure.Authentication;
using Acme.Tests.TestSupport.Shared.Application;
using Microsoft.Extensions.Options;

namespace Acme.Tests.Api.Filters;

public class ContextCommandFilterTests
{
    [Fact]
    public async Task OnActionExecutionAsync_WithAnAuthenticatedUser_Should_FillTheContextFromTheClaims()
    {
        var fakeContextCommand = new FakeContextCommand();
        var context = ActionExecutingContextBuilder.Create().WithUser("user-1", "user-1@localhost").WithArgument("command", fakeContextCommand).Build();

        await new ContextCommandFilter(Options.Create(FakeAuthSettings.Create())).OnActionExecutionAsync(context, ActionExecutingContextBuilder.NextOf(context));

        Assert.Equal(["user-1", "user-1@localhost"], [fakeContextCommand.Context.User.Id, fakeContextCommand.Context.User.Email]);
    }

    [Fact]
    public async Task OnActionExecutionAsync_Should_TakeTheCorrelationIdOfTheRequest()
    {
        var correlationId = Guid.CreateVersion7();
        var fakeContextCommand = new FakeContextCommand();
        var context = ActionExecutingContextBuilder.Create().WithCorrelationId(correlationId).WithArgument("command", fakeContextCommand).Build();

        await new ContextCommandFilter(Options.Create(FakeAuthSettings.Create())).OnActionExecutionAsync(context, ActionExecutingContextBuilder.NextOf(context));

        Assert.Equal(correlationId, fakeContextCommand.Context.CorrelationId);
    }

    [Fact]
    public async Task OnActionExecutionAsync_WithoutAUser_Should_ReplaceWhatTheClientSent()
    {
        var fakeContextCommand = new FakeContextCommand { Context = new CommandContext { User = new AuditUser("spoofed", "spoofed@localhost") } };
        var context = ActionExecutingContextBuilder.Create().WithArgument("command", fakeContextCommand).Build();

        await new ContextCommandFilter(Options.Create(FakeAuthSettings.Create())).OnActionExecutionAsync(context, ActionExecutingContextBuilder.NextOf(context));

        Assert.Equal([null, null], [fakeContextCommand.Context.User.Id, fakeContextCommand.Context.User.Email]);
    }

    [Fact]
    public async Task OnActionExecutionAsync_Should_LeaveOtherArgumentsUntouchedAndRunTheAction()
    {
        var context = ActionExecutingContextBuilder.Create().WithUser("user-1", "user-1@localhost").WithArgument("id", Guid.Empty).WithArgument("command", new FakeCommand()).Build();
        var ran = false;

        await new ContextCommandFilter(Options.Create(FakeAuthSettings.Create())).OnActionExecutionAsync(context, () =>
        {
            ran = true;
            return ActionExecutingContextBuilder.NextOf(context)();
        });

        Assert.True(ran);
        Assert.Equal(Guid.Empty, context.ActionArguments["id"]);
    }
}
