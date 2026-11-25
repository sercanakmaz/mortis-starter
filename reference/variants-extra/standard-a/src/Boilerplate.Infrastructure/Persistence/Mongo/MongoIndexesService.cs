using Microsoft.Extensions.Hosting;

namespace Boilerplate.Infrastructure.Persistence.Mongo;

public class MongoIndexesService(MongoIndexes mongoIndexes, IHostEnvironment hostEnvironment) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        return hostEnvironment.IsDevelopment() ? mongoIndexes.EnsureAsync() : Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
