using Microsoft.Extensions.DependencyInjection;

namespace Acme.Domain;

public static class DomainModule
{
    public static IServiceCollection AddDomainModule(this IServiceCollection services)
    {
        return services;
    }
}
