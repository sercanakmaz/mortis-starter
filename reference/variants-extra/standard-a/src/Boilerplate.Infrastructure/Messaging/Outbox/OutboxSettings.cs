namespace Boilerplate.Infrastructure.Messaging.Outbox;

public class OutboxSettings
{
    public const string Section = "Outbox";

    public int PollingIntervalMilliseconds { get; set; } = 1000;
}
