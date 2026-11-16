using Microsoft.Extensions.DependencyInjection;

namespace Boilerplate.Domain.Aggregates.Orders;

public static class OrderModule
{
    public static IServiceCollection AddOrdersModule(this IServiceCollection services)
    {
        services.AddScoped<IOrderService, OrderService>();

        return services;
    }
}
