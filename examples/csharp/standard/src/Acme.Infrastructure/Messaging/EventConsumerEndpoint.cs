using System.Text.RegularExpressions;
using Acme.Shared.Domain;

namespace Acme.Infrastructure.Messaging;

public partial class EventConsumerEndpoint(Type consumerType)
{
    private const string ConsumerSuffix = "EventConsumer";

    public Type ConsumerType { get; } = consumerType;

    public Type EventType { get; } = EventTypeOf(consumerType);

    public string Queue { get; private set; } = QueueOf(consumerType);

    public int RetryCount { get; private set; } = 3;

    public EventConsumerEndpoint Retry(int retryCount)
    {
        RetryCount = retryCount;

        return this;
    }

    public EventConsumerEndpoint Named(string queue)
    {
        Queue = queue;

        return this;
    }

    public static string QueueOf(Type consumerType)
    {
        var name = consumerType.Name.EndsWith(ConsumerSuffix) ? consumerType.Name[..^ConsumerSuffix.Length] : consumerType.Name;

        return WordStart().Replace(name, "-$1").ToLowerInvariant();
    }

    public static Type EventTypeOf(Type consumerType)
    {
        for (var type = consumerType; type is not null; type = type.BaseType)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(BaseEventConsumer<>)) return type.GetGenericArguments()[0];
        }

        throw new ArgumentException($"{consumerType.Name} is not a {nameof(BaseEventConsumer<>)}.");
    }

    [GeneratedRegex("(?<!^)([A-Z])")]
    private static partial Regex WordStart();
}
