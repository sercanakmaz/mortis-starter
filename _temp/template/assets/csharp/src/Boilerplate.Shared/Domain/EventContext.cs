namespace Boilerplate.Shared.Domain;

public sealed class EventContext
{
    private readonly Lock _lock = new();
    private readonly Queue<EventBase> _raisedEvents = new();
    private readonly Queue<EventBase> _dispatchedEvents = new();

    public int RaisedCount
    {
        get
        {
            lock (_lock)
            {
                return _raisedEvents.Count;
            }
        }
    }

    public void AddRaised(EventBase @event)
    {
        lock (_lock)
        {
            _raisedEvents.Enqueue(@event);
        }
    }

    public EventBase TakeRaised()
    {
        lock (_lock)
        {
            return _raisedEvents.TryDequeue(out var @event) ? @event : null;
        }
    }

    public void AddDispatched(EventBase @event)
    {
        lock (_lock)
        {
            _dispatchedEvents.Enqueue(@event);
        }
    }

    public EventBase TakeDispatched()
    {
        lock (_lock)
        {
            return _dispatchedEvents.TryDequeue(out var @event) ? @event : null;
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _raisedEvents.Clear();
            _dispatchedEvents.Clear();
        }
    }
}
