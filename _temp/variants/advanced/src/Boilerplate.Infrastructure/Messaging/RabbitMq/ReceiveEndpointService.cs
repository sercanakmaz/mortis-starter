using System.Diagnostics;
using Boilerplate.Shared.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Boilerplate.Infrastructure.Logging;
using Boilerplate.Infrastructure.Tenancy;
using RabbitMQ.Client.Events;
using System.Globalization;
using Boilerplate.Shared.Persistence;
using Boilerplate.Shared.Domain;
using RabbitMQ.Client;

namespace Boilerplate.Infrastructure.Messaging.RabbitMq;

public class ReceiveEndpointService(EventConsumerEndpoint eventConsumerEndpoint, RabbitMqConnection rabbitMqConnection, IServiceScopeFactory serviceScopeFactory, ILogger<ReceiveEndpointService> logger) : BackgroundService
{
    private readonly string _errorQueue = eventConsumerEndpoint.Queue + RabbitMqConstants.ErrorSuffix;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeAsync(stoppingToken);
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(exception, "Receive endpoint {Queue} cannot reach RabbitMQ; it tries again in {Seconds} seconds", eventConsumerEndpoint.Queue, RabbitMqConstants.ReconnectSeconds);

                await Task.Delay(TimeSpan.FromSeconds(RabbitMqConstants.ReconnectSeconds), stoppingToken);
            }
        }
    }

    private async Task ConsumeAsync(CancellationToken stoppingToken)
    {
        await using var channel = await rabbitMqConnection.CreateChannelAsync(false, stoppingToken);
        var queueArguments = new Dictionary<string, object> { [RabbitMqConstants.QueueTypeArgument] = RabbitMqConstants.ClassicQueueType };

        foreach (var queue in new[] { eventConsumerEndpoint.Queue, _errorQueue })
        {
            await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false, arguments: queueArguments, cancellationToken: stoppingToken);
            await channel.ExchangeDeclareAsync(queue, ExchangeType.Fanout, durable: true, autoDelete: false, cancellationToken: stoppingToken);
            await channel.QueueBindAsync(queue, queue, string.Empty, cancellationToken: stoppingToken);
        }

        var exchange = MessageNames.Exchange(eventConsumerEndpoint.EventType);
        var urn = MessageNames.Urn(eventConsumerEndpoint.EventType);

        await channel.ExchangeDeclareAsync(exchange, ExchangeType.Fanout, durable: true, autoDelete: false, cancellationToken: stoppingToken);
        await channel.ExchangeBindAsync(eventConsumerEndpoint.Queue, exchange, string.Empty, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (_, delivery) =>
        {
            var envelope = MessageEnvelope.Read(delivery.Body.Span);
            var eventType = eventConsumerEndpoint.EventType;

            if (!envelope.MessageType.Contains(urn))
            {
                logger.LogWarning("No consumer on {Queue} for {MessageType}; the message is acknowledged", eventConsumerEndpoint.Queue, string.Join(", ", envelope.MessageType));
                await channel.BasicAckAsync(delivery.DeliveryTag, false, stoppingToken);
                return;
            }

            var @event = envelope.ToEvent(eventType);
            ActivityContext.TryParse(envelope.Headers.GetValueOrDefault(RabbitMqConstants.TraceParentHeader)?.ToString(), null, out var parent);
            using var activity = Telemetry.Source.StartActivity($"consume {eventType.Name}", ActivityKind.Consumer, parent);
            using var logScope = logger.BeginScope(LogFields.Of(@event));

            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    await ConsumeAsync(@event);

                    logger.LogInformation("Handled {Event} {EventId} by {EventConsumer}", eventType.Name, @event.EventId, eventConsumerEndpoint.ConsumerType.FullName);
                    break;
                }
                catch (Exception exception) when (attempt <= eventConsumerEndpoint.RetryCount)
                {
                    Telemetry.ConsumerFailures.Add(1, new KeyValuePair<string, object>("consumer", eventConsumerEndpoint.ConsumerType.Name));
                    logger.LogWarning(exception, "Event consumer {EventConsumer} failed for {Event} {EventId}, attempt {Attempt}", eventConsumerEndpoint.ConsumerType.FullName, eventType.Name, @event.EventId, attempt);
                }
                catch (Exception exception)
                {
                    Telemetry.ConsumerFailures.Add(1, new KeyValuePair<string, object>("consumer", eventConsumerEndpoint.ConsumerType.Name));
                    logger.LogError(exception, "Event consumer {EventConsumer} failed for {Event} {EventId}; moved to {ErrorQueue}", eventConsumerEndpoint.ConsumerType.FullName, eventType.Name, @event.EventId, _errorQueue);

                    var faultProperties = new BasicProperties(delivery.BasicProperties)
                    {
                        Headers = new Dictionary<string, object>(delivery.BasicProperties.Headers ?? new Dictionary<string, object>())
                        {
                            ["MT-Reason"] = "fault",
                            ["MT-Fault-ExceptionType"] = exception.GetType().FullName,
                            ["MT-Fault-Message"] = exception.Message,
                            ["MT-Fault-StackTrace"] = exception.StackTrace,
                            ["MT-Fault-Timestamp"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                            ["MT-Fault-ConsumerType"] = eventConsumerEndpoint.ConsumerType.FullName,
                            ["MT-Fault-InputAddress"] = RabbitMqConstants.AddressPrefix + eventConsumerEndpoint.Queue,
                            ["MT-Fault-MessageType"] = eventType.FullName,
                            ["MT-Host-MachineName"] = Environment.MachineName
                        }
                    };

                    await channel.BasicPublishAsync(_errorQueue, string.Empty, false, faultProperties, delivery.Body, stoppingToken);
                    break;
                }
            }

            await channel.BasicAckAsync(delivery.DeliveryTag, false, stoppingToken);
        };

        await channel.BasicQosAsync(0, RabbitMqConstants.PrefetchCount, false, stoppingToken);
        await channel.BasicConsumeAsync(eventConsumerEndpoint.Queue, autoAck: false, consumer, stoppingToken);

        while (channel.IsOpen)
        {
            await Task.Delay(TimeSpan.FromSeconds(RabbitMqConstants.ReconnectSeconds), stoppingToken);
        }
    }

    private async Task ConsumeAsync(EventBase @event)
    {
        using var scope = serviceScopeFactory.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantScope>().Enter(@event.TenantId);
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var eventConsumer = (IEventConsumer)scope.ServiceProvider.GetRequiredService(eventConsumerEndpoint.ConsumerType);

        try
        {
            await unitOfWork.BeginTransactionAsync(nameof(ReceiveEndpointService));
            await eventConsumer.Handle(@event);
            await unitOfWork.CommitTransactionAsync(nameof(ReceiveEndpointService));
        }
        catch (Exception)
        {
            await unitOfWork.RollbackTransactionAsync();

            throw;
        }
    }
}
