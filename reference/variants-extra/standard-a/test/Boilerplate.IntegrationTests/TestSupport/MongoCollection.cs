namespace Boilerplate.IntegrationTests.TestSupport;

[CollectionDefinition(IntegrationConstants.MongoCollection)]
public class MongoCollection : ICollectionFixture<MongoFixture>;
