using Boilerplate.Shared.Domain;

namespace Boilerplate.Tests.TestSupport.Shared.Domain;

public class RecordingAsyncEventHandler<TEvent>(string name, int sequence, List<string> calls, int failures = 0, Exception failure = null) : BaseAsyncEventHandler<TEvent> where TEvent : EventBase, IQueueEvent
{
    private int _failed;

    public override int Sequence => sequence;

    protected override Task OnHandle(TEvent @event)
    {
        calls.Add(name);

        if (_failed >= failures) return Task.CompletedTask;

        _failed++;

        throw failure;
    }
}
