using Boilerplate.Domain.Aggregates.Products;
using Boilerplate.Shared.Domain;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Boilerplate.Infrastructure.Persistence.Mongo.Products;

public class ProductRepository(IMongoDatabase database, MongoUnitOfWork unitOfWork, EventContext eventContext) : MongoRepository<Product, Guid>(database, unitOfWork, eventContext, "products"), IProductRepository
{
    public Task<Product> FindBySkuTrackingAsync(string sku)
    {
        return FindOneTrackingAsync(Builders<BsonDocument>.Filter.Eq("sku", sku));
    }
}
