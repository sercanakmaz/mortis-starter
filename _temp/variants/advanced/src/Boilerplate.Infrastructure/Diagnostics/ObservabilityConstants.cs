namespace Boilerplate.Infrastructure.Diagnostics;

public static class ObservabilityConstants
{
    public const string LivePath = "/health/live";
    public const string ReadyPath = "/health/ready";
    public const string ReadyTag = "ready";
    public const string DatabaseCheck = "database";
    public const string OutboxCheck = "outbox";
    public const string BrokerCheck = "broker";
    public const string UnmatchedRoute = "(no route)";
}
