using Boilerplate.Infrastructure.Persistence.Customers;
using Boilerplate.Infrastructure.Persistence.Orders;
using Boilerplate.Infrastructure.Persistence.Products;
using Microsoft.Extensions.DependencyInjection;
using Boilerplate.Domain.Aggregates.Customers;
using Boilerplate.Domain.Aggregates.Products;
using Boilerplate.Domain.Aggregates.Orders;

namespace Boilerplate.Infrastructure.Persistence;

public static class PersistenceModule
{
    public static IServiceCollection AddPersistenceModule(this IServiceCollection services)
    {
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();

        return services;
    }
}
