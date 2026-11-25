using Boilerplate.Domain.Aggregates.Categories;
using Boilerplate.Shared.Domain;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Boilerplate.Infrastructure.Persistence.Mongo.Categories;

public class CategoryRepository(IMongoDatabase database, MongoUnitOfWork unitOfWork, EventContext eventContext) : MongoRepository<Category, int>(database, unitOfWork, eventContext, "categories"), ICategoryRepository;
