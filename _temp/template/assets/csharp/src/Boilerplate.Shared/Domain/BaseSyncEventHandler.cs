namespace Boilerplate.Shared.Domain;

public interface ISyncEventHandler
{
    int Sequence { get; }
    Task Handle(EventBase @event);
}

public abstract class BaseSyncEventHandler<TEvent> : ISyncEventHandler where TEvent : EventBase
{
    public virtual int Sequence => 0;

    protected abstract Task OnHandle(TEvent @event);

    public Task Handle(EventBase @event)
    {
        return OnHandle((TEvent)@event);
    }
}
