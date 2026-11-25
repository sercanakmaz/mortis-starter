using Boilerplate.Domain.Aggregates.Products;
using Boilerplate.Infrastructure.Persistence.Mongo;
using Boilerplate.Infrastructure.Persistence.Mongo.Products;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Boilerplate.Tests.Infrastructure.Persistence.Mongo;

public class ProductReaderTests
{
    [Fact]
    public void Filtered_Should_MatchTheNameIgnoringCaseWithTheTextEscapedAndTheCategory()
    {
        MongoClassMaps.Register();

        var filter = ProductReader.Filtered(new ProductListFilter(1, 10, ProductSort.SkuAscending, "a.b", 3))
            .Render(new RenderArgs<Product>(BsonSerializer.LookupSerializer<Product>(), BsonSerializer.SerializerRegistry));

        Assert.Equal("a\\.b", filter["name"].AsBsonRegularExpression.Pattern);
        Assert.Equal("i", filter["name"].AsBsonRegularExpression.Options);
        Assert.Equal(3, filter["categoryId"].AsInt32);
    }

    [Fact]
    public void SortOf_Should_BreakTiesByIdForTheNameSort()
    {
        MongoClassMaps.Register();

        var sort = ProductReader.SortOf(ProductSort.NameAscending)
            .Render(new RenderArgs<Product>(BsonSerializer.LookupSerializer<Product>(), BsonSerializer.SerializerRegistry));

        Assert.Equal(["name", "_id"], sort.Names);
    }
}
