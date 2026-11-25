using Acme.Shared.Domain;

namespace Acme.Tests.TestSupport.Shared.Domain;

public class RecordingEventConsumer(List<string> calls, int failures = 0, Exception failure = null) : BaseEventConsumer<FakeQueueEvent>
{
    private int _failed;

    protected override Task OnHandle(FakeQueueEvent @event)
    {
        calls.Add(GetType().Name);

        if (_failed >= failures) return Task.CompletedTask;

        _failed++;

        throw failure;
    }
}

public class OtherRecordingEventConsumer(List<string> calls) : RecordingEventConsumer(calls);
