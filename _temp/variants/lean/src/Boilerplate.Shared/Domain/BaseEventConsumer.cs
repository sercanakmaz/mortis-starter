using Boilerplate.Shared.Application;

namespace Boilerplate.Shared.Domain;

public interface IEventConsumer
{
    Task Handle(EventBase @event);
}

public abstract class BaseEventConsumer<TEvent> : IEventConsumer where TEvent : EventBase, IQueueEvent
{
    protected abstract Task OnHandle(TEvent @event);

    public Task Handle(EventBase @event)
    {
        return OnHandle((TEvent)@event);
    }

    protected static CommandContext ContextOf(EventBase @event)
    {
        return CommandContext.FromEvent(@event, new AuditUser(SystemUser.Id, SystemUser.Email));
    }
}
