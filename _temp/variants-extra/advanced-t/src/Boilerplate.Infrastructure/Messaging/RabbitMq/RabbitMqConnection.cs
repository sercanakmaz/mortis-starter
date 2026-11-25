using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;

namespace Boilerplate.Infrastructure.Messaging.RabbitMq;

public sealed class RabbitMqConnection(IConfiguration configuration) : IAsyncDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IConnection _connection;

    public async Task<IChannel> CreateChannelAsync(bool publisherConfirms, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);

        try
        {
            if (_connection is not { IsOpen: true })
            {
                var connectionFactory = new ConnectionFactory
                {
                    Uri = new Uri(configuration.GetConnectionString(RabbitMqConstants.ConnectionStringName)),
                    AutomaticRecoveryEnabled = true
                };

                _connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
            }
        }
        finally
        {
            _lock.Release();
        }

        return await _connection.CreateChannelAsync(new CreateChannelOptions(publisherConfirms, publisherConfirms), cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }
}
