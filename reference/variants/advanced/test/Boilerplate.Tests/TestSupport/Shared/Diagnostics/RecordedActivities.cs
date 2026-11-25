using System.Diagnostics;
using Boilerplate.Shared.Diagnostics;

namespace Boilerplate.Tests.TestSupport.Shared.Diagnostics;

public sealed class RecordedActivities : IDisposable
{
    public const string TestSourceName = "Boilerplate.Tests";

    private readonly ActivityListener _activityListener;

    public RecordedActivities()
    {
        _activityListener = new ActivityListener
        {
            ShouldListenTo = activitySource => activitySource.Name == Telemetry.Name || activitySource.Name == TestSourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = Stopped.Add
        };
        ActivitySource.AddActivityListener(_activityListener);
    }

    public List<Activity> Stopped { get; } = [];
    public ActivitySource TestSource { get; } = new(TestSourceName);

    public void Dispose()
    {
        TestSource.Dispose();
        _activityListener.Dispose();
    }
}
