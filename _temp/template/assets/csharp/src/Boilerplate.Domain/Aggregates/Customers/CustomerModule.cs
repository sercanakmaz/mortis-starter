using Microsoft.Extensions.DependencyInjection;

namespace Boilerplate.Domain.Aggregates.Customers;

public static class CustomerModule
{
    public static IServiceCollection AddCustomersModule(this IServiceCollection services)
    {
        services.AddScoped<ICustomerService, CustomerService>();

        return services;
    }
}
