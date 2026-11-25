namespace Boilerplate.Infrastructure.Scheduling;

public class ScheduledJobRun
{
    public string Job { get; private set; }
    public DateTime Tick { get; private set; }
    public DateTime StartedAt { get; private set; }
    public DateTime? FinishedAt { get; private set; }
    public string Error { get; private set; }
}
