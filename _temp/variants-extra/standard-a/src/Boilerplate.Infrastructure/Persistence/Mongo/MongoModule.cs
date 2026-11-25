using Boilerplate.Domain.Aggregates.Categories;
using Boilerplate.Domain.Aggregates.Customers;
using Boilerplate.Domain.Aggregates.Orders;
using Boilerplate.Domain.Aggregates.Products;
using Boilerplate.Infrastructure.Persistence.Mongo.Categories;
using Boilerplate.Infrastructure.Persistence.Mongo.Customers;
using Boilerplate.Infrastructure.Persistence.Mongo.Orders;
using Boilerplate.Infrastructure.Persistence.Mongo.Products;
using Boilerplate.Shared.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace Boilerplate.Infrastructure.Persistence.Mongo;

public static class MongoModule
{
    public static IServiceCollection AddMongoModule(this IServiceCollection services, IConfiguration configuration)
    {
        MongoClassMaps.Register();

        var mongoUrl = MongoUrl.Create(configuration.GetConnectionString(PersistenceConstants.ConnectionStringName));

        services.AddSingleton<IMongoClient>(new MongoClient(mongoUrl));
        services.AddSingleton(serviceProvider => serviceProvider.GetRequiredService<IMongoClient>().GetDatabase(mongoUrl.DatabaseName));
        services.AddScoped<MongoSession>();
        services.AddScoped<MongoUnitOfWork>();
        services.AddScoped<IUnitOfWork>(serviceProvider => serviceProvider.GetRequiredService<MongoUnitOfWork>());
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<ICategoryReader, CategoryReader>();
        services.AddScoped<ICustomerReader, CustomerReader>();
        services.AddScoped<IOrderReader, OrderReader>();
        services.AddScoped<IProductReader, ProductReader>();
        services.AddSingleton<MongoIndexes>();
        services.AddHostedService<MongoIndexesService>();

        return services;
    }
}
