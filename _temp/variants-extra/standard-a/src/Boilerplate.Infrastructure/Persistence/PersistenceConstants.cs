namespace Boilerplate.Infrastructure.Persistence;

public static class PersistenceConstants
{
    public const string ConnectionStringName = "Default";
    public const string ConnectionStringVariable = "ConnectionStrings__Default";
    public const int UserIdMaxLength = 64;
    public const int UserNameMaxLength = 256;
    public const string VersionElement = "version";
    public const string CountersCollection = "counters";
    public const string OutboxCollection = "outbox_messages";
}
