using Acme.Domain.Aggregates.Categories;
using Acme.Domain.Aggregates.Customers;
using Acme.Domain.Aggregates.Orders;
using Acme.Domain.Aggregates.Products;
using Acme.Infrastructure.Persistence.Categories;
using Acme.Infrastructure.Persistence.Customers;
using Acme.Infrastructure.Persistence.Orders;
using Acme.Infrastructure.Persistence.Products;
using Microsoft.Extensions.DependencyInjection;

namespace Acme.Infrastructure.Persistence;

public static class PersistenceModule
{
    public static IServiceCollection AddPersistenceModule(this IServiceCollection services)
    {
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<ICategoryReader, CategoryReader>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICustomerReader, CustomerReader>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOrderReader, OrderReader>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IProductReader, ProductReader>();

        return services;
    }
}
