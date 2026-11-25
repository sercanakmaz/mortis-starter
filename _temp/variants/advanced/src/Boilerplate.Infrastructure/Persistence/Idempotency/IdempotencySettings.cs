namespace Boilerplate.Infrastructure.Persistence.Idempotency;

public class IdempotencySettings
{
    public const string Section = "Idempotency";

    public int RetentionHours { get; set; } = 24;
    public int InProgressTimeoutSeconds { get; set; } = 60;
}
