using Boilerplate.Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Boilerplate.Shared.Persistence;
using Boilerplate.Shared.Domain;

namespace Boilerplate.Tests.TestSupport.Infrastructure.Messaging;

public class EventPublisherBuilder
{
    private readonly ServiceCollection _services = new();
    private readonly RecordingLogger<EventPublisher> _logger = new();

    public static EventPublisherBuilder Create() => new();

    public EventPublisherBuilder WithUnitOfWork(IUnitOfWork unitOfWork)
    {
        _services.AddScoped(_ => unitOfWork);
        return this;
    }

    public EventPublisherBuilder WithEventHandler<TEvent>(BaseAsyncEventHandler<TEvent> eventHandler) where TEvent : EventBase, IQueueEvent
    {
        _services.AddSingleton(eventHandler);
        return this;
    }

    public EventPublisherBuilder WithScopedEventHandler<TEvent>(Func<IServiceProvider, BaseAsyncEventHandler<TEvent>> factory) where TEvent : EventBase, IQueueEvent
    {
        _services.AddScoped(factory);
        return this;
    }

    public RecordingLogger<EventPublisher> BuildLogger() => _logger;

    public EventPublisher Build()
    {
        var serviceProvider = _services.BuildServiceProvider();

        return new EventPublisher(serviceProvider.GetRequiredService<IServiceScopeFactory>(), _logger);
    }
}
