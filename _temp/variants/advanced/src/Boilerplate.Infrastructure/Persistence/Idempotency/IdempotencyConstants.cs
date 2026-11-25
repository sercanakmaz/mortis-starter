namespace Boilerplate.Infrastructure.Persistence.Idempotency;

public static class IdempotencyConstants
{
    public const string Table = "idempotency_records";
    public const string Header = "Idempotency-Key";
    public const string ReplayedHeader = "Idempotency-Replayed";
    public const string KeyReusedCode = "idempotency_key_reused";
    public const string InProgressCode = "idempotency_in_progress";
    public const int KeyMaxLength = 200;
    public const int ScopeMaxLength = 300;
    public const int HashLength = 64;
    public const int CleanupIntervalMinutes = 60;
}
