using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Boilerplate.Contracts;
using Boilerplate.Infrastructure.Messaging.RabbitMq;
using Boilerplate.Shared.Domain;

namespace Boilerplate.Tests.TestSupport.Architecture;

public static class EventContracts
{
    public const string UpdateVariable = "MORTIS_UPDATE_CONTRACTS";
    public const string FileName = "event-contracts.json";

    public static string Describe()
    {
        var contracts = new SortedDictionary<string, SortedDictionary<string, string>>(StringComparer.Ordinal);

        foreach (var eventType in typeof(ContractsMarker).Assembly.GetTypes().Where(type => !type.IsAbstract && typeof(IQueueEvent).IsAssignableFrom(type)))
        {
            contracts[MessageNames.Exchange(eventType)] = new SortedDictionary<string, string>(
                eventType.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(property => property.CanRead).ToDictionary(property => property.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name ?? property.Name, property => TypeName(property.PropertyType)),
                StringComparer.Ordinal);
        }

        return JsonSerializer.Serialize(contracts, new JsonSerializerOptions { WriteIndented = true }).ReplaceLineEndings("\n");
    }

    public static string ReadPinned([CallerFilePath] string callerPath = "")
    {
        var path = Path.Combine(Path.GetDirectoryName(callerPath)!, FileName);

        if (Environment.GetEnvironmentVariable(UpdateVariable) == "1")
        {
            File.WriteAllText(path, Describe() + "\n");
        }

        return File.Exists(path) ? File.ReadAllText(path).ReplaceLineEndings("\n").TrimEnd('\n') : string.Empty;
    }

    public static string TypeName(Type type) => Nullable.GetUnderlyingType(type) is { } underlying ? TypeName(underlying) + "?" : type.IsGenericType ? $"{type.Name[..type.Name.IndexOf('`')]}<{string.Join(",", type.GetGenericArguments().Select(TypeName))}>" : type.Name;
}
