using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Boilerplate.Contracts.Orders;
using Boilerplate.Infrastructure.Messaging.RabbitMq;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using Xunit;

namespace Interop.Tests;

public class GoLineAddedRecorder : IConsumer<LineAdded>
{
    public static readonly List<ConsumeContext<LineAdded>> Received = [];

    public Task Consume(ConsumeContext<LineAdded> context)
    {
        lock (Received) Received.Add(context);
        return Task.CompletedTask;
    }
}

[Collection("go")]
public class GoInteropTests
{
    private const string Broker = "amqp://guest:guest@localhost:5672/";
    private static readonly string GoBus = Environment.GetEnvironmentVariable("GOBUS") ?? "/tmp/gobus";

    private static RabbitMqConnection OurConnection() =>
        new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string> { ["ConnectionStrings:RabbitMq"] = Broker }).Build());

    private static Process StartGo(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(GoBus) { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
        return Process.Start(startInfo);
    }

    private static async Task<string> RunGo(params string[] arguments)
    {
        using var process = StartGo(arguments);
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, $"gobus {string.Join(' ', arguments)} exited {process.ExitCode}: {error}");
        return output.Trim();
    }

    private static async Task<(Process Process, string Out)> StartGoConsumer(string queue, string sku, params string[] extra)
    {
        await RunGo("declare", "--queue", queue);
        await using (var channel = await OurConnection().CreateChannelAsync(false, CancellationToken.None))
        {
            await channel.QueuePurgeAsync(queue);
            await channel.QueuePurgeAsync(queue + "_error");
        }

        var folder = Directory.CreateTempSubdirectory("gobus").FullName;
        var output = Path.Combine(folder, "event.json");
        var ready = Path.Combine(folder, "ready");
        var process = StartGo(["consume", "--queue", queue, "--out", output, "--ready", ready, "--timeout", "40s", "--sku", sku, .. extra]);
        for (var i = 0; i < 100 && !File.Exists(ready); i++) await Task.Delay(100);
        Assert.True(File.Exists(ready), "the Go endpoint did not start: " + (process.HasExited ? await process.StandardError.ReadToEndAsync() : "still starting"));
        return (process, output);
    }

    private static async Task<JsonElement> Received(string output)
    {
        for (var i = 0; i < 100 && !File.Exists(output); i++) await Task.Delay(100);
        Assert.True(File.Exists(output), "the Go consumer received nothing");
        await Task.Delay(200);
        return JsonDocument.Parse(await File.ReadAllTextAsync(output)).RootElement;
    }

    private static async Task<IBusControl> StartMassTransit(string queue, bool newtonsoft, bool consume = true)
    {
        var services = new ServiceCollection();
        services.AddMassTransit(x =>
        {
            if (consume) x.AddConsumer<GoLineAddedRecorder>();
            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(new Uri("rabbitmq://localhost/"), h => { h.Username("guest"); h.Password("guest"); });
                if (newtonsoft) { cfg.UseNewtonsoftJsonSerializer(); cfg.UseNewtonsoftJsonDeserializer(); }
                if (consume) cfg.ReceiveEndpoint(queue, e => e.ConfigureConsumer<GoLineAddedRecorder>(context));
            });
        });
        var bus = services.BuildServiceProvider().GetRequiredService<IBusControl>();
        await bus.StartAsync();
        return bus;
    }

    [Fact]
    public async Task Go1_TheGoEndpointReceivesWhatOurCSharpPublisherPublishes_WithTheContext()
    {
        var sku = $"SKU-{Guid.NewGuid():N}";
        var (process, output) = await StartGoConsumer("interop-go-from-cs", sku);
        var lineAdded = new LineAdded("order-1", "line-1", sku, 3) { CorrelationId = Guid.CreateVersion7(), TenantId = "acme", UserId = "cs-user", UserEmail = "cs@example.com", Language = "tr" };

        await new RabbitMqPublisher(OurConnection()).PublishAsync(lineAdded, CancellationToken.None);
        var received = await Received(output);
        await process.WaitForExitAsync();

        Assert.Equal(lineAdded.Sku, received.GetProperty("SKU").GetString());
        Assert.Equal(3, received.GetProperty("Quantity").GetInt32());
        Assert.Equal(lineAdded.EventId.ToString(), received.GetProperty("EventID").GetString());
        Assert.Equal(lineAdded.CorrelationId.ToString(), received.GetProperty("CorrelationID").GetString());
        Assert.Equal("acme", received.GetProperty("TenantID").GetString());
        Assert.Equal("cs-user", received.GetProperty("UserID").GetString());
        Assert.Equal("cs@example.com", received.GetProperty("UserEmail").GetString());
        Assert.Equal("tr", received.GetProperty("Language").GetString());
    }

    [Fact]
    public async Task Go2_OurCSharpReaderReadsWhatGoPublishes_WithTheContextInTheBodyAndTheHeaders()
    {
        const string queue = "interop-cs-from-go";
        await using var channel = await OurConnection().CreateChannelAsync(false, CancellationToken.None);
        await channel.ExchangeDeclareAsync(MessageNames.Exchange(typeof(LineAdded)), ExchangeType.Fanout, durable: true, autoDelete: false);
        await channel.QueueDeclareAsync(queue, durable: false, exclusive: true, autoDelete: true);
        await channel.QueueBindAsync(queue, MessageNames.Exchange(typeof(LineAdded)), string.Empty);
        var sku = $"SKU-{Guid.NewGuid():N}";
        var correlationId = Guid.CreateVersion7();

        var eventId = await RunGo("publish", "--sku", sku, "--quantity", "4", "--correlation", correlationId.ToString(), "--tenant", "acme", "--user", "go-user", "--email", "go@example.com");

        BasicGetResult result = null;
        for (var i = 0; i < 50 && result is null; i++) { result = await channel.BasicGetAsync(queue, true); if (result is null) await Task.Delay(100); }
        Assert.NotNull(result);
        var envelope = MessageEnvelope.Read(result.Body.Span);
        var lineAdded = (LineAdded)envelope.ToEvent(typeof(LineAdded));
        Assert.Equal("application/vnd.masstransit+json", result.BasicProperties.ContentType);
        Assert.Equal(eventId, result.BasicProperties.MessageId);
        Assert.Contains(MessageNames.Urn(typeof(LineAdded)), envelope.MessageType);
        Assert.Equal(sku, lineAdded.Sku);
        Assert.Equal(4, lineAdded.Quantity);
        Assert.Equal(Guid.Parse(eventId), lineAdded.EventId);
        Assert.Equal(correlationId, lineAdded.CorrelationId);
        Assert.Null(lineAdded.CausationId);
        Assert.Equal("acme", lineAdded.TenantId);
        Assert.Equal("go-user", lineAdded.UserId);
        Assert.Equal("go@example.com", lineAdded.UserEmail);
        Assert.Equal("acme", envelope.Headers["Mortis-Tenant-Id"].ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Go3_TheGoEndpointReceivesWhatMassTransitPublishes(bool newtonsoft)
    {
        var sku = $"SKU-{Guid.NewGuid():N}";
        var (process, output) = await StartGoConsumer($"interop-go-from-mt-{(newtonsoft ? "newtonsoft" : "stj")}", sku);
        var bus = await StartMassTransit("unused", newtonsoft, consume: false);
        var correlationId = Guid.NewGuid();

        await bus.Publish(new LineAdded("order-1", "line-1", sku, 5) { CorrelationId = correlationId });
        var received = await Received(output);
        await process.WaitForExitAsync();
        await bus.StopAsync();

        Assert.Equal(sku, received.GetProperty("SKU").GetString());
        Assert.Equal(5, received.GetProperty("Quantity").GetInt32());
        Assert.Equal(correlationId.ToString(), received.GetProperty("CorrelationID").GetString());
        Assert.Equal(string.Empty, received.GetProperty("UserID").GetString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Go4_AMassTransitConsumerReceivesWhatGoPublishes(bool newtonsoft)
    {
        var bus = await StartMassTransit($"interop-mt-from-go-{(newtonsoft ? "newtonsoft" : "stj")}", newtonsoft);
        var sku = $"SKU-{Guid.NewGuid():N}";
        var correlationId = Guid.NewGuid();

        var eventId = await RunGo("publish", "--sku", sku, "--quantity", "6", "--correlation", correlationId.ToString(), "--user", "go-user");

        ConsumeContext<LineAdded> received = null;
        for (var i = 0; i < 50 && received is null; i++)
        {
            await Task.Delay(100);
            lock (GoLineAddedRecorder.Received) received = GoLineAddedRecorder.Received.FirstOrDefault(c => c.Message.Sku == sku);
        }
        await bus.StopAsync();

        Assert.NotNull(received);
        Assert.Equal(Guid.Parse(eventId), received.MessageId);
        Assert.Equal(correlationId, received.CorrelationId);
        Assert.Equal(6, received.Message.Quantity);
        Assert.Equal("go-user", received.Message.UserId);
    }

    [Fact]
    public async Task Go5_TheGoEndpointAndAMassTransitEndpointDeclareTheSameQueueWithoutAPreconditionFailure()
    {
        await RunGo("declare", "--queue", "interop-topology-go-first");
        var first = await StartMassTransit("interop-topology-go-first", false);
        await first.StopAsync();

        var second = await StartMassTransit("interop-topology-mt-first", false);
        await second.StopAsync();
        await RunGo("declare", "--queue", "interop-topology-mt-first");

        await RunGo("declare", "--queue", "decrease-stock-when-order-line-added");
    }

    [Fact]
    public async Task Go6_AFailingGoConsumerLeavesTheMessageInTheErrorQueueWithTheFaultHeaders()
    {
        const string queue = "interop-go-fault";
        var sku = $"SKU-{Guid.NewGuid():N}";
        var (process, _) = await StartGoConsumer(queue, sku, "--fail", "--retry", "2");
        await using var channel = await OurConnection().CreateChannelAsync(false, CancellationToken.None);
        var lineAdded = new LineAdded("order-1", "line-1", sku, 1) { CorrelationId = Guid.CreateVersion7() };

        await new RabbitMqPublisher(OurConnection()).PublishAsync(lineAdded, CancellationToken.None);
        await process.WaitForExitAsync();

        BasicGetResult faulted = null;
        for (var i = 0; i < 50 && faulted is null; i++) { faulted = await channel.BasicGetAsync(queue + "_error", true); if (faulted is null) await Task.Delay(100); }
        Assert.NotNull(faulted);
        var headers = faulted.BasicProperties.Headers;
        string Header(string name) => headers[name] is byte[] bytes ? Encoding.UTF8.GetString(bytes) : headers[name]?.ToString();
        Assert.Equal("fault", Header("MT-Reason"));
        Assert.Equal("Go.LineAddedRecorder", Header("MT-Fault-ConsumerType"));
        Assert.Equal("the recorder was told to fail", Header("MT-Fault-Message"));
        Assert.Equal("rabbitmq://localhost/" + queue, Header("MT-Fault-InputAddress"));
        Assert.Contains(lineAdded.Sku, Encoding.UTF8.GetString(faulted.Body.Span));
        Assert.Equal(lineAdded.EventId.ToString(), faulted.BasicProperties.MessageId);
    }
}
