namespace Boilerplate.Infrastructure.Messaging.RabbitMq;

public static class MessageNames
{
    public static string Exchange(Type messageType)
    {
        var name = messageType.IsNested ? $"{messageType.DeclaringType!.Name}+{messageType.Name}" : messageType.Name;

        return $"{messageType.Namespace}:{name}";
    }

    public static string Urn(Type messageType) => RabbitMqConstants.UrnPrefix + Exchange(messageType);

    public static List<string> MessageTypes(Type messageType)
    {
        var types = new List<Type>();

        for (var type = messageType; type is not null && type != typeof(object); type = type.BaseType)
        {
            types.Add(type);
        }

        types.AddRange(messageType.GetInterfaces().Where(contract => contract.Namespace?.StartsWith(nameof(System)) != true));

        return types.Select(Urn).ToList();
    }
}
