using DotNet.Testcontainers.Builders;
using MongoDB.Driver;
using Testcontainers.MongoDb;

namespace Boilerplate.IntegrationTests.TestSupport;

public sealed class MongoFixture : IAsyncLifetime
{
    private MongoDbContainer _container;

    public string ServerConnectionString { get; private set; }
    public string Unavailable { get; private set; }

    public async Task InitializeAsync()
    {
        ServerConnectionString = Environment.GetEnvironmentVariable(IntegrationConstants.MongoVariable);

        if (ServerConnectionString == IntegrationConstants.SkipValue)
        {
            Unavailable = $"{IntegrationConstants.MongoVariable} is {IntegrationConstants.SkipValue}.";
            return;
        }

        if (!string.IsNullOrEmpty(ServerConnectionString))
        {
            return;
        }

        try
        {
            _container = new MongoDbBuilder(IntegrationConstants.MongoImage).WithReplicaSet().Build();
            await _container.StartAsync();
            ServerConnectionString = _container.GetConnectionString();
        }
        catch (Exception exception) when (exception is DockerUnavailableException or HttpRequestException or TimeoutException or ArgumentException)
        {
            Unavailable = $"No MongoDB: set {IntegrationConstants.MongoVariable} or start Docker ({exception.GetType().Name}).";
        }
    }

    public string CreateDatabase(string testClass)
    {
        var mongoUrl = new MongoUrlBuilder(ServerConnectionString) { DatabaseName = $"{IntegrationConstants.DatabasePrefix}{testClass.ToLowerInvariant()}_{Guid.CreateVersion7():N}" };

        if (mongoUrl.Username is not null)
        {
            mongoUrl.AuthenticationSource = IntegrationConstants.AuthenticationDatabase;
        }

        return mongoUrl.ToMongoUrl().Url;
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}
