using Boilerplate.Domain.Aggregates.Orders;
using Boilerplate.Shared.Domain;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Boilerplate.Infrastructure.Persistence.Mongo.Orders;

public class OrderRepository(IMongoDatabase database, MongoUnitOfWork unitOfWork, EventContext eventContext) : MongoRepository<Order, Guid>(database, unitOfWork, eventContext, "orders"), IOrderRepository;
