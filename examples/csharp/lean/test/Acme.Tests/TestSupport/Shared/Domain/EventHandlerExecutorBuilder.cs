using Acme.Shared.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Acme.Tests.TestSupport.Shared.Domain;

public class EventHandlerExecutorBuilder
{
    private readonly ServiceCollection _services = new();
    private EventContext _eventContext = new();

    public static EventHandlerExecutorBuilder Create() => new();

    public EventHandlerExecutorBuilder WithEventContext(EventContext eventContext)
    {
        _eventContext = eventContext;
        return this;
    }

    public EventHandlerExecutorBuilder WithEventHandler<TEvent>(BaseSyncEventHandler<TEvent> eventHandler) where TEvent : EventBase
    {
        _services.AddSingleton(eventHandler);
        return this;
    }

    public EventHandlerExecutor Build()
    {
        return new EventHandlerExecutor(_services.BuildServiceProvider(), _eventContext);
    }
}
