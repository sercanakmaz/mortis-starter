using Boilerplate.Shared.Domain;

namespace Boilerplate.Tests.TestSupport.Shared.Domain;

public class RecordingEventHandler<TEvent>(string name, int sequence, List<string> handledBy, EventContext eventContext = null, EventBase cascadedEvent = null) : BaseSyncEventHandler<TEvent> where TEvent : EventBase
{
    public override int Sequence => sequence;

    protected override async Task OnHandle(TEvent @event)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(20));

        handledBy.Add(name);

        if (cascadedEvent is not null) eventContext.AddRaised(cascadedEvent);
    }
}

public class ThrowingEventHandler<TEvent> : BaseSyncEventHandler<TEvent> where TEvent : EventBase
{
    protected override Task OnHandle(TEvent @event)
    {
        throw new InvalidOperationException("a sync handler vetoes the transaction");
    }
}
