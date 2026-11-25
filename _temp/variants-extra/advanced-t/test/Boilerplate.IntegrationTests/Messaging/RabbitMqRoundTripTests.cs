using Boilerplate.Contracts.Orders;
using Boilerplate.Domain.Aggregates.Orders.Events;
using Boilerplate.Infrastructure.Messaging.RabbitMq;
using Boilerplate.IntegrationTests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;

namespace Boilerplate.IntegrationTests.Messaging;

[Collection(IntegrationConstants.RabbitMqCollection)]
public class RabbitMqRoundTripTests(RabbitMqFixture rabbitMqFixture, PostgresFixture postgresFixture)
{
    [SkippableFact]
    public async Task PublishAsync_Should_DeliverTheEnvelopeToAQueueBoundToTheEventsExchange()
    {
        Skip.If(rabbitMqFixture.Unavailable is not null, rabbitMqFixture.Unavailable);
        Skip.If(postgresFixture.Unavailable is not null, postgresFixture.Unavailable);
        await using var serviceProvider = IntegrationServices.Create(await postgresFixture.CreateDatabaseAsync(nameof(RabbitMqRoundTripTests)), rabbitMqFixture.Uri);
        var rabbitMqConnection = serviceProvider.GetRequiredService<RabbitMqConnection>();
        await using var channel = await rabbitMqConnection.CreateChannelAsync(false, CancellationToken.None);
        var exchange = MessageNames.Exchange(typeof(LineAdded));
        await channel.ExchangeDeclareAsync(exchange, ExchangeType.Fanout, durable: true, autoDelete: false);
        var queue = (await channel.QueueDeclareAsync(string.Empty, durable: false, exclusive: true, autoDelete: true)).QueueName;
        await channel.QueueBindAsync(queue, exchange, string.Empty);

        await serviceProvider.GetRequiredService<RabbitMqPublisher>().PublishAsync(new LineAdded("order-1", "line-1", "SKU-1", 2), CancellationToken.None);

        var delivery = await RabbitMqDeliveries.NextAsync(channel, queue);
        Assert.NotNull(delivery);
        Assert.Equal(RabbitMqConstants.ContentType, delivery.BasicProperties.ContentType);
        Assert.Contains("SKU-1", System.Text.Encoding.UTF8.GetString(delivery.Body.Span));
    }
}
