using Boilerplate.Api.Filters;
using Boilerplate.Tests.TestSupport.Api;
using Boilerplate.Tests.TestSupport.Shared.Application;

namespace Boilerplate.Tests.Api.Filters;

public class UserCommandFilterTests
{
    [Fact]
    public async Task OnActionExecutionAsync_WithAnAuthenticatedUser_Should_FillTheUserCommandFromTheClaims()
    {
        var fakeUserCommand = new FakeUserCommand();
        var context = ActionExecutingContextBuilder.Create().WithUser("user-1", "user-1@localhost").WithArgument("command", fakeUserCommand).Build();

        await new UserCommandFilter().OnActionExecutionAsync(context, ActionExecutingContextBuilder.NextOf(context));

        Assert.Equal(["user-1", "user-1@localhost"], [fakeUserCommand.UserId, fakeUserCommand.UserEmail]);
    }

    [Fact]
    public async Task OnActionExecutionAsync_WithoutAUser_Should_ClearWhatTheClientSent()
    {
        var fakeUserCommand = new FakeUserCommand { UserId = "spoofed", UserEmail = "spoofed@localhost" };
        var context = ActionExecutingContextBuilder.Create().WithArgument("command", fakeUserCommand).Build();

        await new UserCommandFilter().OnActionExecutionAsync(context, ActionExecutingContextBuilder.NextOf(context));

        Assert.Equal([null, null], [fakeUserCommand.UserId, fakeUserCommand.UserEmail]);
    }

    [Fact]
    public async Task OnActionExecutionAsync_Should_LeaveOtherArgumentsUntouchedAndRunTheAction()
    {
        var context = ActionExecutingContextBuilder.Create().WithUser("user-1", "user-1@localhost").WithArgument("id", Guid.Empty).WithArgument("command", new FakeCommand()).Build();
        var ran = false;

        await new UserCommandFilter().OnActionExecutionAsync(context, () =>
        {
            ran = true;
            return ActionExecutingContextBuilder.NextOf(context)();
        });

        Assert.True(ran);
        Assert.Equal(Guid.Empty, context.ActionArguments["id"]);
    }
}
