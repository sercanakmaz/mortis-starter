namespace Boilerplate.IntegrationTests.TestSupport;

[CollectionDefinition(IntegrationConstants.RabbitMqCollection)]
public class RabbitMqCollection : ICollectionFixture<RabbitMqFixture>, ICollectionFixture<PostgresFixture>;
