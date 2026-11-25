using Acme.Shared.Application;

namespace Acme.Shared.Domain;

public sealed class EventContext
{
    private readonly Lock _lock = new();
    private readonly Queue<EventBase> _raisedEvents = new();
    private readonly Queue<EventBase> _dispatchedEvents = new();
    private readonly Stack<CommandContext> _commandContexts = new();

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

    public void EnterCommand(CommandContext commandContext)
    {
        lock (_lock)
        {
            _commandContexts.Push(commandContext);
        }
    }

    public void LeaveCommand()
    {
        lock (_lock)
        {
            _commandContexts.TryPop(out _);
        }
    }

    public void AddRaised(EventBase @event)
    {
        lock (_lock)
        {
            _raisedEvents.Enqueue(_commandContexts.TryPeek(out var commandContext)
                ? @event with
                {
                    CorrelationId = commandContext.CorrelationId,
                    CausationId = commandContext.CausationId,
                    TenantId = commandContext.TenantId,
                    Language = commandContext.Language
                }
                : @event);
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
