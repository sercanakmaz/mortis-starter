namespace Boilerplate.Infrastructure.Messaging.RabbitMq;

public static class RabbitMqConstants
{
    public const string ConnectionStringName = "RabbitMq";
    public const string ContentType = "application/vnd.masstransit+json";
    public const string UrnPrefix = "urn:message:";
    public const string AddressPrefix = "rabbitmq://localhost/";
    public const string ErrorSuffix = "_error";
    public const string QueueTypeArgument = "x-queue-type";
    public const string ClassicQueueType = "classic";
    public const ushort PrefetchCount = 16;
    public const int ReconnectSeconds = 5;
    public const string TraceParentHeader = "traceparent";
}
