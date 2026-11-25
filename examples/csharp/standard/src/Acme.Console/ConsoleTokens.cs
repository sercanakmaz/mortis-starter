using System.Text.Json;
using Acme.Infrastructure.Authentication;
using Acme.Shared.Exceptions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Acme.Console;

public class ConsoleTokens(AuthSettings authSettings, IHostEnvironment hostEnvironment)
{
    public string WriteDevelopmentToken(string argument)
    {
        if (!hostEnvironment.IsDevelopment())
        {
            throw new ForbiddenException("Writing a development token outside the Development environment");
        }

        if (string.IsNullOrEmpty(authSettings.SigningKey))
        {
            throw new InvalidParameterException("Auth:SigningKey", "a development signing key is required");
        }

        var developmentUser = JsonSerializer.Deserialize<DevelopmentUser>(string.IsNullOrWhiteSpace(argument) ? "{}" : argument, ConsoleConstants.InputOptions);
        var claims = new Dictionary<string, object>
        {
            [authSettings.SubjectClaim] = developmentUser.Subject ?? throw new InvalidParameterException("subject", "a development token needs a subject"),
            [authSettings.EmailClaim] = developmentUser.Email
        };

        if (!string.IsNullOrEmpty(developmentUser.Language))
        {
            claims[authSettings.LanguageClaim] = developmentUser.Language;
        }

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = authSettings.Issuer,
            Audience = authSettings.Audience,
            Claims = claims,
            Expires = DateTime.UtcNow.AddHours(ConsoleConstants.DevelopmentTokenHours),
            SigningCredentials = new SigningCredentials(authSettings.SigningSecurityKey(), SecurityAlgorithms.HmacSha256)
        });
    }

    private sealed record DevelopmentUser(string Subject, string Email, string Language);
}
