using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Acme.Application.Categories;
using Acme.Application.Customers;
using Acme.Application.Orders;
using Acme.Application.Products;
using Acme.Domain.Aggregates.Orders.Events;
using Acme.Shared.Application;
using Acme.Shared.Domain;

namespace Acme.Application;

public static class ApplicationModule
{
    public static IServiceCollection AddApplicationModule(this IServiceCollection services)
    {
        services.AddCommandHandler<CreateCategoryCommand, CategoryDto, CreateCategoryCommandHandler>();
        services.AddScoped<IValidator<CreateCategoryCommand>, CreateCategoryCommandValidator>();
        services.AddCommandHandler<GetCategoryCommand, CategoryDto, GetCategoryCommandHandler>();

        services.AddCommandHandler<CreateCustomerCommand, CustomerDto, CreateCustomerCommandHandler>();
        services.AddScoped<IValidator<CreateCustomerCommand>, CreateCustomerCommandValidator>();
        services.AddCommandHandler<BlockCustomerCommand, CustomerDto, BlockCustomerCommandHandler>();
        services.AddCommandHandler<GetCustomerCommand, CustomerDto, GetCustomerCommandHandler>();
        services.AddCommandHandler<RegisterCompletedOrderCommand, CustomerDto, RegisterCompletedOrderCommandHandler>();
        services.AddScoped<BaseSyncEventHandler<Completed>, RegisterCompletedOrderWhenOrderCompletedEventHandler>();

        services.AddCommandHandler<CreateOrderCommand, OrderDto, CreateOrderCommandHandler>();
        services.AddScoped<IValidator<CreateOrderCommand>, CreateOrderCommandValidator>();
        services.AddCommandHandler<AddOrderLineCommand, OrderDto, AddOrderLineCommandHandler>();
        services.AddScoped<IValidator<AddOrderLineCommand>, AddOrderLineCommandValidator>();
        services.AddCommandHandler<CancelOrderLineCommand, OrderDto, CancelOrderLineCommandHandler>();
        services.AddScoped<IValidator<CancelOrderLineCommand>, CancelOrderLineCommandValidator>();
        services.AddCommandHandler<CompleteOrderCommand, OrderDto, CompleteOrderCommandHandler>();
        services.AddCommandHandler<GetOrderCommand, OrderDto, GetOrderCommandHandler>();
        services.AddCommandHandler<CancelOrderWhenAllLinesCancelledCommand, OrderDto, CancelOrderWhenAllLinesCancelledCommandHandler>();
        services.AddScoped<BaseSyncEventHandler<LineCancelled>, CancelOrderWhenOrderLineCancelledEventHandler>();

        services.AddCommandHandler<CreateProductCommand, ProductDto, CreateProductCommandHandler>();
        services.AddScoped<IValidator<CreateProductCommand>, CreateProductCommandValidator>();
        services.AddCommandHandler<GetProductCommand, ProductDto, GetProductCommandHandler>();
        services.AddCommandHandler<IncreaseProductStockCommand, ProductDto, IncreaseProductStockCommandHandler>();
        services.AddScoped<IValidator<IncreaseProductStockCommand>, IncreaseProductStockCommandValidator>();
        services.AddCommandHandler<DecreaseProductStockCommand, ProductDto, DecreaseProductStockCommandHandler>();
        services.AddScoped<IValidator<DecreaseProductStockCommand>, DecreaseProductStockCommandValidator>();

        return services;
    }
}
