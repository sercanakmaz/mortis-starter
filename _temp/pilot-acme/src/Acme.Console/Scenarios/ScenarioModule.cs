using Acme.Application.Customers;
using Acme.Application.Orders;
using Acme.Application.Products;
using Microsoft.Extensions.DependencyInjection;

namespace Acme.Console.Scenarios;

public static class ScenarioModule
{
    public static IServiceCollection AddScenariosModule(this IServiceCollection services)
    {
        services.AddScoped<IScenario, OrderFlowScenario>();
        services.AddScoped<IScenario, StockFlowScenario>();
        services.AddScoped<IScenario, BlockedCustomerScenario>();

        services.AddCommandScenario<CreateCustomerCommand, CustomerDto>();
        services.AddCommandScenario<BlockCustomerCommand, CustomerDto>();
        services.AddCommandScenario<GetCustomerCommand, CustomerDto>();
        services.AddCommandScenario<RegisterCompletedOrderCommand, CustomerDto>();

        services.AddCommandScenario<CreateOrderCommand, OrderDto>();
        services.AddCommandScenario<AddOrderLineCommand, OrderDto>();
        services.AddCommandScenario<CancelOrderLineCommand, OrderDto>();
        services.AddCommandScenario<CompleteOrderCommand, OrderDto>();
        services.AddCommandScenario<GetOrderCommand, OrderDto>();
        services.AddCommandScenario<CancelOrderWhenAllLinesCancelledCommand, OrderDto>();

        services.AddCommandScenario<CreateProductCommand, ProductDto>();
        services.AddCommandScenario<GetProductCommand, ProductDto>();
        services.AddCommandScenario<IncreaseProductStockCommand, ProductDto>();
        services.AddCommandScenario<DecreaseProductStockCommand, ProductDto>();

        return services;
    }
}
