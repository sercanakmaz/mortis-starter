using Boilerplate.Tests.TestSupport.Infrastructure.Authentication;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.IdentityModel.JsonWebTokens;
using Boilerplate.Shared.Exceptions;
using Microsoft.Extensions.Hosting;
using Boilerplate.Console;

namespace Boilerplate.Tests.Console;

public class ConsoleTokensTests
{
    [Fact]
    public async Task WriteDevelopmentToken_InDevelopment_Should_WriteATokenSignedWithTheDevelopmentKeyCarryingTheClaims()
    {
        var consoleTokens = new ConsoleTokens(FakeAuthSettings.Create(), new HostingEnvironment { EnvironmentName = Environments.Development });

        var tokenValidationResult = await new JsonWebTokenHandler().ValidateTokenAsync(consoleTokens.WriteDevelopmentToken("{\"subject\":\"user-1\",\"email\":\"user-1@localhost\",\"tenant\":\"tenant-1\",\"language\":\"tr\"}"), FakeAuthSettings.Create().ValidationParameters());

        Assert.True(tokenValidationResult.IsValid);
        Assert.Equal(["user-1", "user-1@localhost", "tenant-1", "tr"], new[] { "sub", "email", "tenant_id", "locale" }.Select(claim => tokenValidationResult.Claims[claim].ToString()));
    }

    [Fact]
    public void WriteDevelopmentToken_OutsideDevelopment_Should_ThrowForbidden()
    {
        var consoleTokens = new ConsoleTokens(FakeAuthSettings.Create(), new HostingEnvironment { EnvironmentName = Environments.Production });

        Assert.Throws<ForbiddenException>(() => consoleTokens.WriteDevelopmentToken("{\"subject\":\"user-1\"}"));
    }

    [Fact]
    public void WriteDevelopmentToken_WithoutASubject_Should_ThrowInvalidParameter()
    {
        var consoleTokens = new ConsoleTokens(FakeAuthSettings.Create(), new HostingEnvironment { EnvironmentName = Environments.Development });

        Assert.Throws<InvalidParameterException>(() => consoleTokens.WriteDevelopmentToken(null));
    }
}
