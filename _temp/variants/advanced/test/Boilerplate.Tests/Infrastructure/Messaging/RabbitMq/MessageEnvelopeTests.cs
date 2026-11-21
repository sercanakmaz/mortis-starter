using Boilerplate.Infrastructure.Messaging.RabbitMq;
using Boilerplate.Domain.Aggregates.Orders.Events;
using System.Text;

namespace Boilerplate.Tests.Infrastructure.Messaging.RabbitMq;

public class MessageEnvelopeTests
{
    [Fact]
    public void From_Should_UseTheEventIdAsMessageIdAndTheCorrelationAsCorrelationAndConversation()
    {
        var lineAdded = new LineAdded("order-1", "line-1", "SKU-1", 3) { CorrelationId = Guid.CreateVersion7(), CausationId = Guid.CreateVersion7(), TenantId = "tenant-1" };

        var envelope = MessageEnvelope.From(lineAdded, "boilerplate-api");

        Assert.Equal(lineAdded.EventId, envelope.MessageId);
        Assert.Equal([lineAdded.CorrelationId, lineAdded.CorrelationId, lineAdded.CausationId], [envelope.CorrelationId, envelope.ConversationId, envelope.InitiatorId]);
        Assert.Equal("rabbitmq://localhost/Boilerplate.Domain.Aggregates.Orders.Events:LineAdded", envelope.DestinationAddress);
        Assert.Equal("tenant-1", envelope.Headers["Mortis-Tenant-Id"]);
    }

    [Fact]
    public void ToBytes_ThenRead_Should_GiveBackTheSameEvent()
    {
        var lineAdded = new LineAdded("order-1", "line-1", "SKU-1", 3) { CorrelationId = Guid.CreateVersion7() };

        var readBack = MessageEnvelope.Read(MessageEnvelope.From(lineAdded, "boilerplate-api").ToBytes()).ToEvent(typeof(LineAdded));

        Assert.Equal(lineAdded, readBack);
    }

    [Fact]
    public void Read_WithTheBodyMassTransitWritesWithSystemTextJson_Should_GiveTheEventAndItsIds()
    {
        const string body = """
            {"messageId":"19040000-e39c-f26b-89d6-08df47526602","requestId":null,"correlationId":"ca09b075-0317-4992-9052-79b9d6c89880","conversationId":"19040000-e39c-f26b-81dc-08df47526606","initiatorId":null,
             "sourceAddress":"rabbitmq://localhost/probe","destinationAddress":"rabbitmq://localhost/Boilerplate.Domain.Aggregates.Orders.Events:LineAdded","responseAddress":null,"faultAddress":null,
             "messageType":["urn:message:Boilerplate.Domain.Aggregates.Orders.Events:LineAdded","urn:message:Boilerplate.Shared.Domain:EventBase","urn:message:Boilerplate.Shared.Domain:IQueueEvent"],
             "message":{"id":"order-1","lineId":"line-1","sku":"SKU-1","quantity":3,"eventId":"7afaf008-ae39-4cf6-954c-2fefe5e608c1","occurredAt":"2026-11-20T22:05:33.3998Z","correlationId":"ca09b075-0317-4992-9052-79b9d6c89880","causationId":null,"tenantId":null,"userId":null},
             "expirationTime":null,"sentTime":"2026-11-20T22:05:33.4232534Z","headers":{},"host":{"machineName":"vm","processName":"probe","processId":1049,"massTransitVersion":"8.5.12.0"}}
            """;

        var envelope = MessageEnvelope.Read(Encoding.UTF8.GetBytes(body));
        var lineAdded = (LineAdded)envelope.ToEvent(typeof(LineAdded));

        Assert.Equal(Guid.Parse("19040000-e39c-f26b-89d6-08df47526602"), envelope.MessageId);
        Assert.Equal(["SKU-1", "3"], [lineAdded.Sku, lineAdded.Quantity.ToString()]);
        Assert.Equal(Guid.Parse("ca09b075-0317-4992-9052-79b9d6c89880"), lineAdded.CorrelationId);
    }

    [Fact]
    public void Read_WithTheBodyMassTransitWritesWithNewtonsoftAndNoContextFields_Should_TakeTheCorrelationFromTheEnvelope()
    {
        const string body = """
            {"MessageId":"3d040000-e39c-f26b-8410-08df475268b1","CorrelationId":"18ec1e17-9271-40c4-9477-07ea8ca9a2d6","ConversationId":"3d040000-e39c-f26b-1bba-08df475268b5",
             "MessageType":["urn:message:Boilerplate.Domain.Aggregates.Orders.Events:LineAdded"],
             "Message":{"Id":"order-1","LineId":"line-1","Sku":"SKU-1","Quantity":3},"SentTime":"2026-11-20T22:05:37.9254288Z","Headers":{}}
            """;

        var lineAdded = (LineAdded)MessageEnvelope.Read(Encoding.UTF8.GetBytes(body)).ToEvent(typeof(LineAdded));

        Assert.Equal("SKU-1", lineAdded.Sku);
        Assert.Equal(Guid.Parse("18ec1e17-9271-40c4-9477-07ea8ca9a2d6"), lineAdded.CorrelationId);
    }
}
