namespace Acme.Shared.Domain;

public interface IAsyncEventHandler
{
    int Sequence { get; }

    Task Handle(EventBase @event);
}

public abstract class BaseAsyncEventHandler<TEvent> : IAsyncEventHandler where TEvent : EventBase, IQueueEvent
{
    public virtual int Sequence => 0;

    protected abstract Task OnHandle(TEvent @event);

    public Task Handle(EventBase @event)
    {
        return OnHandle((TEvent)@event);
    }
}
