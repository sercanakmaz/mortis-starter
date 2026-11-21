using Boilerplate.Infrastructure.Authentication;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Exceptions;
using Boilerplate.Shared.Domain;
using System.Security.Claims;
using System.Text.Json;

namespace Boilerplate.Console;

public class ConsoleTokens(AuthSettings authSettings, IHostEnvironment hostEnvironment)
{
    public async Task<CommandContext> ContextOfAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return new CommandContext { User = new AuditUser(ConsoleConstants.UserId, ConsoleConstants.UserEmail) };
        }

        var tokenValidationResult = await new JsonWebTokenHandler().ValidateTokenAsync(token, authSettings.ValidationParameters());

        if (!tokenValidationResult.IsValid)
        {
            throw new InvalidParameterException("token", "the token is not valid", tokenValidationResult.Exception);
        }

        var principal = new ClaimsPrincipal(tokenValidationResult.ClaimsIdentity);

        return new CommandContext { User = authSettings.UserOf(principal) };
    }

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
            [authSettings.SubjectClaim] = developmentUser.Subject ?? ConsoleConstants.UserId,
            [authSettings.EmailClaim] = developmentUser.Email ?? ConsoleConstants.UserEmail
        };

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = authSettings.Issuer,
            Audience = authSettings.Audience,
            Claims = claims,
            Expires = DateTime.UtcNow.AddHours(ConsoleConstants.DevelopmentTokenHours),
            SigningCredentials = new SigningCredentials(authSettings.SigningSecurityKey(), SecurityAlgorithms.HmacSha256)
        });
    }

    private sealed record DevelopmentUser(string Subject, string Email);
}
