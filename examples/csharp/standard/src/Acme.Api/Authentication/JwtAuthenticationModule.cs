using Acme.Infrastructure.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Acme.Api.Authentication;

public static class JwtAuthenticationModule
{
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var authSettings = configuration.GetSection(AuthSettings.Section).Get<AuthSettings>() ?? new AuthSettings();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = authSettings.ValidationParameters();
            });
        services.AddAuthorization();

        return services;
    }
}
