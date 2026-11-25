using Boilerplate.Infrastructure.Messaging;
using Xunit;
using Boilerplate.Contracts.Orders;
using Boilerplate.Infrastructure.Messaging.RabbitMq;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;

namespace Interop.Tests;

public class LineAddedRecorder : IConsumer<LineAdded>
{
    public static readonly List<ConsumeContext<LineAdded>> Received = [];

    public Task Consume(ConsumeContext<LineAdded> context)
    {
        lock (Received) Received.Add(context);
        return Task.CompletedTask;
    }
}

public class InteropTests
{
    private const string Broker = "amqp://guest:guest@localhost:5672/";

    private static RabbitMqConnection OurConnection() =>
        new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string> { ["ConnectionStrings:RabbitMq"] = Broker }).Build());

    private static async Task<IBusControl> StartMassTransit(string queue, bool newtonsoft, bool consume = true)
    {
        var services = new ServiceCollection();
        services.AddMassTransit(x =>
        {
            if (consume) x.AddConsumer<LineAddedRecorder>();
            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(new Uri("rabbitmq://localhost/"), h => { h.Username("guest"); h.Password("guest"); });
                if (newtonsoft) { cfg.UseNewtonsoftJsonSerializer(); cfg.UseNewtonsoftJsonDeserializer(); }
                if (consume) cfg.ReceiveEndpoint(queue, e => e.ConfigureConsumer<LineAddedRecorder>(context));
            });
        });
        var bus = services.BuildServiceProvider().GetRequiredService<IBusControl>();
        await bus.StartAsync();
        return bus;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Check1And2_AMassTransitConsumerReceivesWhatWePublish(bool newtonsoft)
    {
        var bus = await StartMassTransit($"interop-mt-{(newtonsoft ? "newtonsoft" : "stj")}", newtonsoft);
        var lineAdded = new LineAdded("order-1", "line-1", $"SKU-{Guid.NewGuid():N}", 3) { CorrelationId = Guid.CreateVersion7() };

        await new RabbitMqPublisher(OurConnection()).PublishAsync(lineAdded, CancellationToken.None);

        ConsumeContext<LineAdded> received = null;
        for (var i = 0; i < 50 && received is null; i++)
        {
            await Task.Delay(100);
            lock (LineAddedRecorder.Received) received = LineAddedRecorder.Received.FirstOrDefault(c => c.Message.Sku == lineAdded.Sku);
        }
        await bus.StopAsync();

        Assert.NotNull(received);
        Assert.Equal(lineAdded.EventId, received.MessageId);
        Assert.Equal(lineAdded.CorrelationId, received.CorrelationId);
        Assert.Equal(3, received.Message.Quantity);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Check3_WeReadWhatMassTransitPublishes(bool newtonsoft)
    {
        var queue = $"interop-raw-{(newtonsoft ? "newtonsoft" : "stj")}";
        await using var channel = await OurConnection().CreateChannelAsync(false, CancellationToken.None);
        await channel.ExchangeDeclareAsync(MessageNames.Exchange(typeof(LineAdded)), ExchangeType.Fanout, durable: true, autoDelete: false);
        await channel.QueueDeclareAsync(queue, durable: false, exclusive: true, autoDelete: true);
        await channel.QueueBindAsync(queue, MessageNames.Exchange(typeof(LineAdded)), string.Empty);
        var bus = await StartMassTransit("unused", newtonsoft, consume: false);
        var sku = $"SKU-{Guid.NewGuid():N}";
        var correlationId = Guid.NewGuid();

        await bus.Publish(new LineAdded("order-1", "line-1", sku, 3) { CorrelationId = correlationId });

        BasicGetResult result = null;
        for (var i = 0; i < 50; i++)
        {
            result = await channel.BasicGetAsync(queue, true);
            if (result is not null && MessageEnvelope.Read(result.Body.Span).Message.ToString().Contains(sku)) break;
            await Task.Delay(100);
        }
        await bus.StopAsync();

        var envelope = MessageEnvelope.Read(result!.Body.Span);
        var lineAdded = (LineAdded)envelope.ToEvent(typeof(LineAdded));
        Assert.Contains(MessageNames.Urn(typeof(LineAdded)), envelope.MessageType);
        Assert.Equal(sku, lineAdded.Sku);
        Assert.Equal(correlationId, lineAdded.CorrelationId);
        Assert.NotNull(envelope.MessageId);
    }

    [Fact]
    public async Task Check4_OurEndpointAndAMassTransitEndpointDeclareTheSameQueueWithoutAPreconditionFailure()
    {
        const string queue = "decrease-stock-when-order-line-added";
        var bus = await StartMassTransit(queue, false);
        await bus.StopAsync();
        var eventConsumerEndpoint = new EventConsumerEndpoint(typeof(Boilerplate.Application.Products.DecreaseStockWhenOrderLineAddedEventConsumer));
        var service = new ReceiveEndpointService(eventConsumerEndpoint, OurConnection(), new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), NullLogger<ReceiveEndpointService>.Instance);
        using var cancellation = new CancellationTokenSource();

        await service.StartAsync(cancellation.Token);
        await Task.Delay(2000);
        await using var channel = await OurConnection().CreateChannelAsync(false, CancellationToken.None);
        var declared = await channel.QueueDeclarePassiveAsync(queue);
        await cancellation.CancelAsync();
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(1u, declared.ConsumerCount);
        var again = await StartMassTransit(queue, false);
        await again.StopAsync();
    }
}
