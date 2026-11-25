using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Boilerplate.Infrastructure.Diagnostics;

public class OutboxHealthCheck(OutboxLagProbe outboxLagProbe, IOptions<ObservabilitySettings> settings) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await outboxLagProbe.RefreshAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("The outbox cannot be measured.", exception);
        }

        return outboxLagProbe.OldestAgeSeconds > settings.Value.OutboxLagThresholdSeconds
            ? HealthCheckResult.Unhealthy($"The oldest unpublished event is {outboxLagProbe.OldestAgeSeconds:F0} s old.")
            : HealthCheckResult.Healthy();
    }
}
