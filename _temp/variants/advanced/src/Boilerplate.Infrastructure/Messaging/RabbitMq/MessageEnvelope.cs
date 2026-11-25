using Boilerplate.Shared.Domain;
using System.Text.Json.Serialization;
using System.Diagnostics;
using System.Text.Json;

namespace Boilerplate.Infrastructure.Messaging.RabbitMq;

public class MessageEnvelope
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = JsonIgnoreCondition.Never };

    public Guid? MessageId { get; set; }
    public Guid? RequestId { get; set; }
    public Guid? CorrelationId { get; set; }
    public Guid? ConversationId { get; set; }
    public Guid? InitiatorId { get; set; }
    public string SourceAddress { get; set; }
    public string DestinationAddress { get; set; }
    public string ResponseAddress { get; set; }
    public string FaultAddress { get; set; }
    public List<string> MessageType { get; set; } = [];
    public JsonElement Message { get; set; }
    public DateTime? ExpirationTime { get; set; }
    public DateTime? SentTime { get; set; }
    public Dictionary<string, object> Headers { get; set; } = [];
    public Dictionary<string, object> Host { get; set; } = [];

    public static MessageEnvelope From(EventBase @event, string sourceAddress)
    {
        var eventType = @event.GetType();
        var process = Process.GetCurrentProcess();

        return new MessageEnvelope
        {
            MessageId = @event.EventId,
            CorrelationId = @event.CorrelationId,
            ConversationId = @event.CorrelationId,
            InitiatorId = @event.CausationId,
            SourceAddress = RabbitMqConstants.AddressPrefix + sourceAddress,
            DestinationAddress = RabbitMqConstants.AddressPrefix + MessageNames.Exchange(eventType),
            MessageType = MessageNames.MessageTypes(eventType),
            Message = JsonSerializer.SerializeToElement(@event, eventType, Options),
            SentTime = DateTime.UtcNow,
            Headers = new Dictionary<string, object>
            {
                ["Mortis-Tenant-Id"] = @event.TenantId,
                ["Mortis-User-Id"] = @event.UserId,
                ["Mortis-User-Email"] = @event.UserEmail,
                ["Mortis-Language"] = @event.Language,
                [RabbitMqConstants.TraceParentHeader] = Activity.Current?.Id
            },
            Host = new Dictionary<string, object>
            {
                ["machineName"] = Environment.MachineName,
                ["processName"] = process.ProcessName,
                ["processId"] = Environment.ProcessId,
                ["assembly"] = eventType.Assembly.GetName().Name,
                ["assemblyVersion"] = eventType.Assembly.GetName().Version?.ToString(),
                ["frameworkVersion"] = Environment.Version.ToString(),
                ["operatingSystemVersion"] = Environment.OSVersion.ToString()
            }
        };
    }

    public static MessageEnvelope Read(ReadOnlySpan<byte> body) => JsonSerializer.Deserialize<MessageEnvelope>(body, Options);

    public byte[] ToBytes() => JsonSerializer.SerializeToUtf8Bytes(this, Options);

    public EventBase ToEvent(Type eventType)
    {
        var @event = (EventBase)Message.Deserialize(eventType, Options);

        return @event.CorrelationId == Guid.Empty && CorrelationId is { } correlationId
            ? @event with { CorrelationId = correlationId }
            : @event;
    }
}
