using Boilerplate.Domain.Aggregates.Customers;
using Boilerplate.Shared.Domain;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Boilerplate.Infrastructure.Persistence.Mongo.Customers;

public class CustomerRepository(IMongoDatabase database, MongoUnitOfWork unitOfWork, EventContext eventContext) : MongoRepository<Customer, Guid>(database, unitOfWork, eventContext, "customers"), ICustomerRepository;
