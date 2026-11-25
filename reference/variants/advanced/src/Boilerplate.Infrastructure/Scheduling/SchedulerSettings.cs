namespace Boilerplate.Infrastructure.Scheduling;

public class SchedulerSettings
{
    public const string Section = "Scheduler";
    public const string RunsTable = "scheduled_job_runs";
    public const int JobMaxLength = 100;
    public const int ErrorMaxLength = 2000;

    public int PollSeconds { get; set; } = 15;
    public int CatchUpMinutes { get; set; } = 10;
    public Dictionary<string, SchedulerJobSettings> Jobs { get; set; } = [];
}

public class SchedulerJobSettings
{
    public string Cron { get; set; }
    public string UserId { get; set; }
    public string UserEmail { get; set; }
    public string TenantId { get; set; }
}
