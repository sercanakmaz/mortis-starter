using Boilerplate.Infrastructure.Messaging.RabbitMq;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Boilerplate.Infrastructure.Diagnostics;

public class RabbitMqHealthCheck(RabbitMqConnection rabbitMqConnection) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var channel = await rabbitMqConnection.CreateChannelAsync(false, cancellationToken);

            return HealthCheckResult.Healthy();
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("The broker cannot be reached.", exception);
        }
    }
}
