using Boilerplate.Infrastructure.Persistence.Categories;
using Boilerplate.Infrastructure.Persistence.Customers;
using Boilerplate.Infrastructure.Persistence.Orders;
using Boilerplate.Infrastructure.Persistence.Products;
using Microsoft.Extensions.DependencyInjection;
using Boilerplate.Domain.Aggregates.Categories;
using Boilerplate.Domain.Aggregates.Customers;
using Boilerplate.Domain.Aggregates.Products;
using Boilerplate.Domain.Aggregates.Orders;

namespace Boilerplate.Infrastructure.Persistence;

public static class PersistenceModule
{
    public static IServiceCollection AddPersistenceModule(this IServiceCollection services)
    {
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICategoryReader, CategoryReader>();
        services.AddScoped<ICustomerReader, CustomerReader>();
        services.AddScoped<IOrderReader, OrderReader>();
        services.AddScoped<IProductReader, ProductReader>();

        return services;
    }
}
