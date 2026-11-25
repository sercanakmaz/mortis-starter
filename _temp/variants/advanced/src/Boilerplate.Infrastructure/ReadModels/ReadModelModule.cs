using Boilerplate.Contracts.Orders;
using Microsoft.Extensions.DependencyInjection;
using Boilerplate.Domain.Aggregates.Orders;
using Boilerplate.Domain.Aggregates.Orders.Events;
using Boilerplate.Infrastructure.ReadModels.OrderSummaries;
using Boilerplate.Shared.Domain;

namespace Boilerplate.Infrastructure.ReadModels;

public static class ReadModelModule
{
    public static IServiceCollection AddReadModelModule(this IServiceCollection services)
    {
        services.AddScoped<OrderSummaryProjection>();
        services.AddScoped<IOrderSummaryReader, OrderSummaryReader>();
        services.AddOrderSummaryRefresh<Created>(@event => @event.Id);
        services.AddOrderSummaryRefresh<LineAdded>(@event => @event.Id);
        services.AddOrderSummaryRefresh<LineCancelled>(@event => @event.Id);
        services.AddOrderSummaryRefresh<Completed>(@event => @event.Id);
        services.AddOrderSummaryRefresh<Cancelled>(@event => @event.Id);

        return services;
    }

    private static void AddOrderSummaryRefresh<TEvent>(this IServiceCollection services, Func<TEvent, string> orderIdOf) where TEvent : EventBase
    {
        services.AddScoped<BaseSyncEventHandler<TEvent>>(serviceProvider => new OrderSummaryRefresh<TEvent>(serviceProvider.GetRequiredService<OrderSummaryProjection>(), orderIdOf));
    }
}
