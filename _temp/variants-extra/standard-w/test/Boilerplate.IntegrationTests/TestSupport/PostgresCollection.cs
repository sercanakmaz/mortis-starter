namespace Boilerplate.IntegrationTests.TestSupport;

[CollectionDefinition(IntegrationConstants.PostgresCollection)]
public class PostgresCollection : ICollectionFixture<PostgresFixture>;
