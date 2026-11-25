namespace Boilerplate.Infrastructure.Diagnostics;

public class ObservabilitySettings
{
    public const string Section = "Observability";

    public string OtlpEndpoint { get; set; }
    public int OutboxLagThresholdSeconds { get; set; } = 60;
}
