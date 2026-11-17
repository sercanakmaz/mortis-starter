using Acme.Infrastructure.Messaging;
using Acme.Shared.Domain;
using Acme.Shared.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Acme.Tests.TestSupport.Infrastructure.Messaging;

public class AsyncEventHandlerDispatcherBuilder
{
    private readonly ServiceCollection _services = new();
    private readonly RecordingLogger<AsyncEventHandlerDispatcher> _logger = new();

    public static AsyncEventHandlerDispatcherBuilder Create() => new();

    public AsyncEventHandlerDispatcherBuilder WithUnitOfWork(IUnitOfWork unitOfWork)
    {
        _services.AddScoped(_ => unitOfWork);
        return this;
    }

    public AsyncEventHandlerDispatcherBuilder WithEventHandler<TEvent>(BaseAsyncEventHandler<TEvent> eventHandler) where TEvent : EventBase, IQueueEvent
    {
        _services.AddSingleton(eventHandler);
        return this;
    }

    public AsyncEventHandlerDispatcherBuilder WithScopedEventHandler<TEvent>(Func<IServiceProvider, BaseAsyncEventHandler<TEvent>> factory) where TEvent : EventBase, IQueueEvent
    {
        _services.AddScoped(factory);
        return this;
    }

    public RecordingLogger<AsyncEventHandlerDispatcher> BuildLogger() => _logger;

    public AsyncEventHandlerDispatcher Build()
    {
        var serviceProvider = _services.BuildServiceProvider();

        return new AsyncEventHandlerDispatcher(serviceProvider.GetRequiredService<IServiceScopeFactory>(), _logger);
    }
}
