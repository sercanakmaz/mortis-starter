using Microsoft.Extensions.DependencyInjection;

namespace Boilerplate.Domain.Aggregates.Products;

public static class ProductModule
{
    public static IServiceCollection AddProductsModule(this IServiceCollection services)
    {
        services.AddScoped<IProductService, ProductService>();

        return services;
    }
}
