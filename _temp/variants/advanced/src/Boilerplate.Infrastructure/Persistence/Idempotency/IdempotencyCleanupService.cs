using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Boilerplate.Infrastructure.Persistence.Idempotency;

public class IdempotencyCleanupService(IdempotencyStore idempotencyStore, ILogger<IdempotencyCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var deleted = await idempotencyStore.DeleteExpiredAsync();

                logger.LogInformation("Deleted {Count} expired idempotency records", deleted);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(exception, "Deleting expired idempotency records failed; it tries again after the interval");
            }

            await Task.Delay(TimeSpan.FromMinutes(IdempotencyConstants.CleanupIntervalMinutes), stoppingToken);
        }
    }
}
