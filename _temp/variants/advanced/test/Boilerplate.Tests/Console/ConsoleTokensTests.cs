using Boilerplate.Tests.TestSupport.Infrastructure.Authentication;
using Microsoft.Extensions.Hosting.Internal;
using Boilerplate.Shared.Exceptions;
using Microsoft.Extensions.Hosting;
using Boilerplate.Console;

namespace Boilerplate.Tests.Console;

public class ConsoleTokensTests
{
    [Fact]
    public async Task ContextOfAsync_WithoutAToken_Should_BeTheConsoleUserWithoutATenant()
    {
        var commandContext = await new ConsoleTokens(FakeAuthSettings.Create(), new HostingEnvironment { EnvironmentName = Environments.Production }).ContextOfAsync(null);

        Assert.Equal([ConsoleConstants.UserId, ConsoleConstants.UserEmail, null], [commandContext.User.Id, commandContext.User.Email, commandContext.TenantId]);
    }

    [Fact]
    public async Task WriteDevelopmentToken_InDevelopment_Should_WriteATokenThatContextOfAsyncReadsBack()
    {
        var consoleTokens = new ConsoleTokens(FakeAuthSettings.Create(), new HostingEnvironment { EnvironmentName = Environments.Development });
        var tenantId = Guid.CreateVersion7().ToString();

        var commandContext = await consoleTokens.ContextOfAsync(consoleTokens.WriteDevelopmentToken($"{{\"subject\":\"user-1\",\"email\":\"user-1@localhost\",\"tenant\":\"{tenantId}\"}}"));

        Assert.Equal(["user-1", "user-1@localhost", tenantId], [commandContext.User.Id, commandContext.User.Email, commandContext.TenantId]);
    }

    [Fact]
    public void WriteDevelopmentToken_OutsideDevelopment_Should_ThrowForbidden()
    {
        var consoleTokens = new ConsoleTokens(FakeAuthSettings.Create(), new HostingEnvironment { EnvironmentName = Environments.Production });

        Assert.Throws<ForbiddenException>(() => consoleTokens.WriteDevelopmentToken(null));
    }

    [Fact]
    public async Task ContextOfAsync_WithATokenThatDoesNotValidate_Should_ThrowInvalidParameterForTheToken()
    {
        var consoleTokens = new ConsoleTokens(FakeAuthSettings.Create(), new HostingEnvironment { EnvironmentName = Environments.Development });

        var exception = await Assert.ThrowsAsync<InvalidParameterException>(() => consoleTokens.ContextOfAsync("not.a.token"));

        Assert.StartsWith("token is invalid", exception.Message);
    }
}
