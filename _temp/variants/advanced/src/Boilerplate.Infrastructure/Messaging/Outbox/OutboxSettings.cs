namespace Boilerplate.Infrastructure.Messaging.Outbox;

public class OutboxSettings
{
    public const string Section = "Outbox";

    public int PollingIntervalMilliseconds { get; set; } = 1000;
    public int MaxAttempts { get; set; } = 10;
}
