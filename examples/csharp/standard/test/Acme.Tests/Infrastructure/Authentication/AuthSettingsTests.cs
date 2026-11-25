using System.Security.Claims;
using Acme.Infrastructure.Authentication;
using Acme.Tests.TestSupport.Infrastructure.Authentication;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Acme.Tests.Infrastructure.Authentication;

public class AuthSettingsTests
{
    [Fact]
    public void UserOf_Should_ReadTheConfiguredClaims()
    {
        var authSettings = new AuthSettings { SubjectClaim = "uid", EmailClaim = "mail" };
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("uid", "user-1"), new Claim("mail", "user-1@localhost")], "test"));

        var user = authSettings.UserOf(principal);

        Assert.Equal(["user-1", "user-1@localhost"], [user.Id, user.Email]);
    }

    [Fact]
    public async Task ValidationParameters_WithASigningKey_Should_AcceptATokenSignedWithItAndRefuseAnother()
    {
        var authSettings = FakeAuthSettings.Create();

        var valid = await new JsonWebTokenHandler().ValidateTokenAsync(FakeAuthSettings.TokenSignedWith(FakeAuthSettings.SigningKey), authSettings.ValidationParameters());
        var forged = await new JsonWebTokenHandler().ValidateTokenAsync(FakeAuthSettings.TokenSignedWith(FakeAuthSettings.SigningKey + "-forged"), authSettings.ValidationParameters());

        Assert.True(valid.IsValid);
        Assert.False(forged.IsValid);
    }
}
