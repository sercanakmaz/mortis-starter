using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;

namespace Boilerplate.Shared.Application;

public sealed class QueryDispatcher(IServiceProvider serviceProvider) : IQueryDispatcher
{
    private static readonly ConcurrentDictionary<Type, object> QueryHandlerInvokers = new();

    public Task<TResult> Ask<TResult>(IQuery<TResult> query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var queryHandlerInvoker = (QueryHandlerInvoker<TResult>)QueryHandlerInvokers.GetOrAdd(
            query.GetType(),
            queryType => Activator.CreateInstance(typeof(QueryHandlerInvoker<,>).MakeGenericType(queryType, typeof(TResult))));

        return queryHandlerInvoker.Invoke(serviceProvider, query);
    }
}

public abstract class QueryHandlerInvoker<TResult>
{
    public abstract Task<TResult> Invoke(IServiceProvider serviceProvider, IQuery<TResult> query);
}

public sealed class QueryHandlerInvoker<TQuery, TResult> : QueryHandlerInvoker<TResult> where TQuery : IQuery<TResult>
{
    public override Task<TResult> Invoke(IServiceProvider serviceProvider, IQuery<TResult> query)
    {
        return serviceProvider.GetRequiredService<BaseQueryHandler<TQuery, TResult>>().Handle((TQuery)query);
    }
}
