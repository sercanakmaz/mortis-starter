using Boilerplate.Contracts.Products;
using Boilerplate.Contracts.Orders;
using Boilerplate.Domain.Aggregates.Orders;
using Microsoft.Extensions.DependencyInjection;
using Boilerplate.Domain.Aggregates.Orders.Events;
using Boilerplate.Domain.Aggregates.Products.Events;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Domain;
using Boilerplate.Application.Categories;
using Boilerplate.Application.Customers;
using Boilerplate.Application.Orders;
using Boilerplate.Application.Products;
using FluentValidation;

namespace Boilerplate.Application;

public static class ApplicationModule
{
    public static IServiceCollection AddApplicationModule(this IServiceCollection services)
    {
        services.AddCommandHandler<CreateCategoryCommand, CategoryDto, CreateCategoryCommandHandler>();
        services.AddScoped<IValidator<CreateCategoryCommand>, CreateCategoryCommandValidator>();
        services.AddQueryHandler<GetCategoryQuery, CategoryDto, GetCategoryQueryHandler>();

        services.AddCommandHandler<CreateCustomerCommand, CustomerDto, CreateCustomerCommandHandler>();
        services.AddCommandHandler<SetCustomerTaxNumberCommand, CustomerDto, SetCustomerTaxNumberCommandHandler>();
        services.AddScoped<IValidator<SetCustomerTaxNumberCommand>, SetCustomerTaxNumberCommandValidator>();
        services.AddScoped<IValidator<CreateCustomerCommand>, CreateCustomerCommandValidator>();
        services.AddCommandHandler<BlockCustomerCommand, CustomerDto, BlockCustomerCommandHandler>();
        services.AddCommandHandler<ChangeCustomerCommand, CustomerDto, ChangeCustomerCommandHandler>();
        services.AddScoped<IValidator<ChangeCustomerCommand>, ChangeCustomerCommandValidator>();
        services.AddQueryHandler<GetCustomerQuery, CustomerDto, GetCustomerQueryHandler>();
        services.AddCommandHandler<RegisterCompletedOrderCommand, CustomerDto, RegisterCompletedOrderCommandHandler>();
        services.AddCommandHandler<CancelEmptyOrdersCommand, int, CancelEmptyOrdersCommandHandler>();
        services.AddScoped<BaseSyncEventHandler<Completed>, RegisterCompletedOrderWhenOrderCompletedEventHandler>();

        services.AddCommandHandler<CreateOrderCommand, OrderDto, CreateOrderCommandHandler>();
        services.AddScoped<IValidator<CreateOrderCommand>, CreateOrderCommandValidator>();
        services.AddCommandHandler<AddOrderLineCommand, OrderDto, AddOrderLineCommandHandler>();
        services.AddScoped<IValidator<AddOrderLineCommand>, AddOrderLineCommandValidator>();
        services.AddCommandHandler<CancelOrderLineCommand, OrderDto, CancelOrderLineCommandHandler>();
        services.AddScoped<IValidator<CancelOrderLineCommand>, CancelOrderLineCommandValidator>();
        services.AddCommandHandler<CompleteOrderCommand, OrderDto, CompleteOrderCommandHandler>();
        services.AddQueryHandler<GetOrderQuery, OrderDto, GetOrderQueryHandler>();
        services.AddQueryHandler<ListOrderSummariesQuery, PagedResult<OrderSummaryView>, ListOrderSummariesQueryHandler>();
        services.AddScoped<IValidator<ListOrderSummariesQuery>, ListOrderSummariesQueryValidator>();
        services.AddCommandHandler<CancelOrderWhenAllLinesCancelledCommand, OrderDto, CancelOrderWhenAllLinesCancelledCommandHandler>();
        services.AddScoped<BaseSyncEventHandler<LineCancelled>, CancelOrderWhenOrderLineCancelledEventHandler>();

        services.AddCommandHandler<CreateProductCommand, ProductDto, CreateProductCommandHandler>();
        services.AddScoped<IValidator<CreateProductCommand>, CreateProductCommandValidator>();
        services.AddCommandHandler<RenameProductCommand, ProductDto, RenameProductCommandHandler>();
        services.AddScoped<IValidator<RenameProductCommand>, RenameProductCommandValidator>();
        services.AddScoped<BaseSyncEventHandler<StockDecreased>, InvalidateProductCacheWhenStockDecreasedEventHandler>();
        services.AddScoped<BaseSyncEventHandler<StockIncreased>, InvalidateProductCacheWhenStockIncreasedEventHandler>();
        services.AddScoped<BaseSyncEventHandler<Renamed>, InvalidateProductCacheWhenRenamedEventHandler>();
        services.AddScoped<BaseSyncEventHandler<Deleted>, InvalidateProductCacheWhenDeletedEventHandler>();
        services.AddScoped<BaseSyncEventHandler<Restored>, InvalidateProductCacheWhenRestoredEventHandler>();
        services.AddCommandHandler<DeleteProductCommand, ProductDto, DeleteProductCommandHandler>();
        services.AddCommandHandler<RestoreProductCommand, ProductDto, RestoreProductCommandHandler>();
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
