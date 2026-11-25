using Boilerplate.Shared.Domain;
using RabbitMQ.Client;

namespace Boilerplate.Infrastructure.Messaging.RabbitMq;

public class RabbitMqPublisher(RabbitMqConnection rabbitMqConnection)
{
    private static readonly string Source = AppDomain.CurrentDomain.FriendlyName.ToLowerInvariant();

    public async Task PublishAsync(EventBase @event, CancellationToken cancellationToken)
    {
        var exchange = MessageNames.Exchange(@event.GetType());
        var envelope = MessageEnvelope.From(@event, Source);

        await using var channel = await rabbitMqConnection.CreateChannelAsync(true, cancellationToken);

        await channel.ExchangeDeclareAsync(exchange, ExchangeType.Fanout, durable: true, autoDelete: false, cancellationToken: cancellationToken);

        var basicProperties = new BasicProperties
        {
            ContentType = RabbitMqConstants.ContentType,
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = envelope.MessageId.ToString(),
            CorrelationId = envelope.CorrelationId.ToString()
        };

        await channel.BasicPublishAsync(exchange, string.Empty, false, basicProperties, envelope.ToBytes(), cancellationToken);
    }
}
