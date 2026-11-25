using MongoDB.Driver;

namespace Boilerplate.Infrastructure.Persistence.Mongo;

public class MongoSession(IMongoClient mongoClient) : IDisposable
{
    public IClientSessionHandle Handle { get; private set; }

    public async Task<IClientSessionHandle> StartAsync()
    {
        return Handle ??= await mongoClient.StartSessionAsync();
    }

    public void Dispose()
    {
        Handle?.Dispose();
    }
}
