using Acme.Application.Categories;
using Acme.Application.Customers;
using Acme.Application.Orders;
using Acme.Application.Products;
using Acme.Domain.Aggregates.Orders.Events;
using Acme.Shared.Application;
using Acme.Shared.Domain;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Acme.Application;

public static class ApplicationModule
{
    public static IServiceCollection AddApplicationModule(this IServiceCollection services)
    {
        services.AddCommandHandler<CreateCategoryCommand, CategoryDto, CreateCategoryCommandHandler>();
        services.AddScoped<IValidator<CreateCategoryCommand>, CreateCategoryCommandValidator>();
        services.AddQueryHandler<GetCategoryQuery, CategoryDto, GetCategoryQueryHandler>();

        services.AddCommandHandler<CreateCustomerCommand, CustomerDto, CreateCustomerCommandHandler>();
        services.AddScoped<IValidator<CreateCustomerCommand>, CreateCustomerCommandValidator>();
        services.AddCommandHandler<BlockCustomerCommand, CustomerDto, BlockCustomerCommandHandler>();
        services.AddCommandHandler<ChangeCustomerCommand, CustomerDto, ChangeCustomerCommandHandler>();
        services.AddScoped<IValidator<ChangeCustomerCommand>, ChangeCustomerCommandValidator>();
        services.AddQueryHandler<GetCustomerQuery, CustomerDto, GetCustomerQueryHandler>();
        services.AddCommandHandler<RegisterCompletedOrderCommand, CustomerDto, RegisterCompletedOrderCommandHandler>();
        services.AddScoped<BaseSyncEventHandler<Completed>, RegisterCompletedOrderWhenOrderCompletedEventHandler>();

        services.AddCommandHandler<CreateOrderCommand, OrderDto, CreateOrderCommandHandler>();
        services.AddScoped<IValidator<CreateOrderCommand>, CreateOrderCommandValidator>();
        services.AddCommandHandler<AddOrderLineCommand, OrderDto, AddOrderLineCommandHandler>();
        services.AddScoped<IValidator<AddOrderLineCommand>, AddOrderLineCommandValidator>();
        services.AddCommandHandler<CancelOrderLineCommand, OrderDto, CancelOrderLineCommandHandler>();
        services.AddScoped<IValidator<CancelOrderLineCommand>, CancelOrderLineCommandValidator>();
        services.AddCommandHandler<CompleteOrderCommand, OrderDto, CompleteOrderCommandHandler>();
        services.AddQueryHandler<GetOrderQuery, OrderDto, GetOrderQueryHandler>();
        services.AddCommandHandler<CancelOrderWhenAllLinesCancelledCommand, OrderDto, CancelOrderWhenAllLinesCancelledCommandHandler>();
        services.AddScoped<BaseSyncEventHandler<LineCancelled>, CancelOrderWhenOrderLineCancelledEventHandler>();

        services.AddCommandHandler<CreateProductCommand, ProductDto, CreateProductCommandHandler>();
        services.AddScoped<IValidator<CreateProductCommand>, CreateProductCommandValidator>();
        services.AddQueryHandler<GetProductQuery, ProductDto, GetProductQueryHandler>();
        services.AddQueryHandler<ListProductsQuery, PagedResult<ProductDto>, ListProductsQueryHandler>();
        services.AddScoped<IValidator<ListProductsQuery>, ListProductsQueryValidator>();
        services.AddCommandHandler<IncreaseProductStockCommand, ProductDto, IncreaseProductStockCommandHandler>();
        services.AddScoped<IValidator<IncreaseProductStockCommand>, IncreaseProductStockCommandValidator>();
        services.AddCommandHandler<DecreaseProductStockCommand, ProductDto, DecreaseProductStockCommandHandler>();
        services.AddScoped<IValidator<DecreaseProductStockCommand>, DecreaseProductStockCommandValidator>();

        return services;
    }
}
