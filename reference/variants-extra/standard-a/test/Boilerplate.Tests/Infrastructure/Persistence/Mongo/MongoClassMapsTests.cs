using Boilerplate.Infrastructure.Persistence;
using Boilerplate.Infrastructure.Persistence.Mongo;
using Boilerplate.Tests.TestSupport.Domain.Orders;
using Boilerplate.Tests.TestSupport.Domain.Products;
using Boilerplate.Domain.Aggregates.Orders;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace Boilerplate.Tests.Infrastructure.Persistence.Mongo;

public class MongoClassMapsTests
{
    [Fact]
    public void ToBsonDocument_Should_StoreTheIdAsAStandardUuid()
    {
        MongoClassMaps.Register();
        var product = FakeProduct.Create("SKU-1", 10);

        var id = product.ToBsonDocument()["_id"].AsBsonBinaryData;

        Assert.Equal(BsonBinarySubType.UuidStandard, id.SubType);
        Assert.Equal(product.Id.ToByteArray(bigEndian: true), id.Bytes);
    }

    [Fact]
    public void ToBsonDocument_Should_UseCamelCaseNamesAndEmbedTheOrderLines()
    {
        MongoClassMaps.Register();
        var order = FakeOrder.Create().WithLine("SKU-1", quantity: 2);

        var document = order.ToBsonDocument();

        Assert.True(document.Contains("orderNumber"));
        Assert.Single(document["lines"].AsBsonArray);
        Assert.False(document.Contains("totalAmount"));
        Assert.False(document.Contains("domainEvents"));
    }

    [Fact]
    public void Deserialize_Should_ReadBackAnOrderWithItsLinesFromADocumentWithAVersion()
    {
        MongoClassMaps.Register();
        var order = FakeOrder.Create().WithLine("SKU-1", quantity: 2);
        var document = order.ToBsonDocument();
        document[PersistenceConstants.VersionElement] = 3L;

        var read = BsonSerializer.Deserialize<Order>(document);

        Assert.Equal([("SKU-1", 2)], read.Lines.Select(line => (line.Sku, line.Quantity)));
    }
}
