using Microsoft.Extensions.DependencyInjection;
using Boilerplate.Application.Categories;
using Boilerplate.Application.Customers;
using Boilerplate.Application.Products;
using Boilerplate.Application.Orders;

namespace Boilerplate.Console.Scenarios;

public static class ScenarioModule
{
    public static IServiceCollection AddScenariosModule(this IServiceCollection services)
    {
        services.AddScoped<IScenario, OrderFlowScenario>();
        services.AddScoped<IScenario, StockFlowScenario>();
        services.AddScoped<IScenario, BlockedCustomerScenario>();

        services.AddCommandScenario<CreateCategoryCommand, CategoryDto>();
        services.AddCommandScenario<GetCategoryCommand, CategoryDto>();

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
