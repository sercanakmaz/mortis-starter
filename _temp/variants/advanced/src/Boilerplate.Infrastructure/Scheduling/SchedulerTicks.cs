using Cronos;

namespace Boilerplate.Infrastructure.Scheduling;

public static class SchedulerTicks
{
    public static DateTime? Due(string cron, DateTime now, int catchUpMinutes)
    {
        var tick = CronExpression.Parse(cron).GetOccurrences(now.AddMinutes(-catchUpMinutes), now, fromInclusive: true, toInclusive: true).LastOrDefault();

        return tick == default ? null : tick;
    }
}
