using System.Linq.Expressions;
using Boilerplate.Application;
using Boilerplate.Domain;
using Boilerplate.Infrastructure;
using Boilerplate.Infrastructure.Persistence;
using Boilerplate.Shared;
using Boilerplate.Shared.Application;
using Boilerplate.Infrastructure.Persistence.Mongo;
using MongoDB.Driver;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerplate.IntegrationTests.TestSupport;

public static class IntegrationServices
{
    public static ServiceProvider Create(string connectionString)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string> { [$"ConnectionStrings:{PersistenceConstants.ConnectionStringName}"] = connectionString })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSharedModule().AddDomainModule().AddApplicationModule().AddInfrastructureModule(configuration);

        return services.BuildServiceProvider();
    }

    public static async Task<TContent> Send<TContent>(this ServiceProvider serviceProvider, ICommand<TContent> command)
    {
        using var scope = serviceProvider.CreateScope();

        var commandResult = await scope.ServiceProvider.GetRequiredService<ICommandDispatcher>().Send(command);

        return commandResult.Content;
    }

    public static async Task<long> CountAsync<TDocument>(this ServiceProvider serviceProvider, string collectionName, Expression<Func<TDocument, bool>> predicate)
    {
        await serviceProvider.GetRequiredService<MongoIndexes>().EnsureAsync();

        return await serviceProvider.GetRequiredService<IMongoDatabase>().GetCollection<TDocument>(collectionName).CountDocumentsAsync(predicate);
    }
}
