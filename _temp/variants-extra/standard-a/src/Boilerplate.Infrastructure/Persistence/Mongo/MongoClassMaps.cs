using Boilerplate.Domain.Aggregates.Orders;
using System.Reflection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;

namespace Boilerplate.Infrastructure.Persistence.Mongo;

public static class MongoClassMaps
{
    private const string OrderLinesField = "_lines";
    private static readonly Lazy<bool> Registered = new(RegisterAll);

    public static void Register()
    {
        _ = Registered.Value;
    }

    private static bool RegisterAll()
    {
        BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
        BsonSerializer.RegisterSerializer(new DecimalSerializer(BsonType.Decimal128));

        ConventionRegistry.Register("mortis", new ConventionPack
        {
            new CamelCaseElementNameConvention(),
            new IgnoreExtraElementsConvention(true),
            new EnumRepresentationConvention(BsonType.String)
        }, _ => true);

        BsonClassMap.RegisterClassMap<Order>(map =>
        {
            map.AutoMap();
            map.MapField(OrderLinesField).SetElementName("lines");
            map.MapCreator((List<OrderLine> lines) => WithLines(lines), OrderLinesField);
        });

        return true;
    }

    private static Order WithLines(List<OrderLine> lines)
    {
        var order = (Order)Activator.CreateInstance(typeof(Order), nonPublic: true);

        typeof(Order).GetField(OrderLinesField, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(order, lines ?? []);

        return order;
    }
}
