using Boilerplate.Infrastructure.Authentication;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Boilerplate.Tests.TestSupport.Infrastructure.Authentication;

public static class FakeAuthSettings
{
    public const string SigningKey = "unit-test-signing-key-0123456789-abcdefghij";

    public static AuthSettings Create() => new() { Issuer = "issuer", Audience = "audience", SigningKey = SigningKey };

    public static string TokenSignedWith(string signingKey)
    {
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "issuer",
            Audience = "audience",
            Claims = new Dictionary<string, object> { ["sub"] = "user-1" },
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)), SecurityAlgorithms.HmacSha256)
        });
    }
}
