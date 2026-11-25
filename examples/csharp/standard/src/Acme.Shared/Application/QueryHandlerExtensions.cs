using Microsoft.Extensions.DependencyInjection;

namespace Acme.Shared.Application;

public static class QueryHandlerExtensions
{
    public static IServiceCollection AddQueryHandler<TQuery, TResult, THandler>(this IServiceCollection services)
        where TQuery : IQuery<TResult>
        where THandler : BaseQueryHandler<TQuery, TResult>
    {
        services.AddScoped<BaseQueryHandler<TQuery, TResult>, THandler>();

        return services;
    }
}
