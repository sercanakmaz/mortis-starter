using Boilerplate.Infrastructure.Scheduling;

namespace Boilerplate.Tests.Infrastructure.Scheduling;

public class SchedulerTicksTests
{
    [Theory]
    [InlineData("0 * * * *", "2026-01-01T10:00:30Z", 10, "2026-01-01T10:00:00Z")]
    [InlineData("0 * * * *", "2026-01-01T10:30:00Z", 10, null)]
    [InlineData("*/5 * * * *", "2026-01-01T10:17:00Z", 10, "2026-01-01T10:15:00Z")]
    public void Due_Should_ReturnTheLatestTickInsideTheCatchUpWindow(string cron, string now, int catchUpMinutes, string expected)
    {
        var tick = SchedulerTicks.Due(cron, DateTime.Parse(now).ToUniversalTime(), catchUpMinutes);

        Assert.Equal(expected is null ? null : DateTime.Parse(expected).ToUniversalTime(), tick);
    }
}
