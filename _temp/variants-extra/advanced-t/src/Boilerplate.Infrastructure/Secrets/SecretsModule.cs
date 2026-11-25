using Boilerplate.Shared.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerplate.Infrastructure.Secrets;

public static class SecretsModule
{
    public static IServiceCollection AddSecrets(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SecretsSettings>()
            .Bind(configuration.GetSection(SecretsSettings.Section))
            .Validate(settings => settings.HasUsableCurrentKey(), $"{SecretsSettings.Section}:CurrentKeyId must name a {SecretsConstants.KeyBytes}-byte base64 key in {SecretsSettings.Section}:Keys.")
            .ValidateOnStart();
        services.AddSingleton<ISecretProtector, EnvelopeSecretProtector>();

        return services;
    }
}
