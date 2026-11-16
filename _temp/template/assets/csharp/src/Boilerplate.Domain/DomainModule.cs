using Microsoft.Extensions.DependencyInjection;
using Boilerplate.Domain.Aggregates.Customers;
using Boilerplate.Domain.Aggregates.Products;
using Boilerplate.Domain.Aggregates.Orders;

namespace Boilerplate.Domain;

public static class DomainModule
{
    public static IServiceCollection AddDomainModule(this IServiceCollection services)
    {
        services.AddCustomersModule();
        services.AddOrdersModule();
        services.AddProductsModule();

        return services;
    }
}
