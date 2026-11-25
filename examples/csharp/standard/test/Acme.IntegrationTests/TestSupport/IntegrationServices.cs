using System.Linq.Expressions;
using Acme.Application;
using Acme.Domain;
using Acme.Infrastructure;
using Acme.Infrastructure.Persistence;
using Acme.Shared;
using Acme.Shared.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Acme.IntegrationTests.TestSupport;

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

    public static async Task<int> CountAsync<TEntity>(this ServiceProvider serviceProvider, Expression<Func<TEntity, bool>> predicate) where TEntity : class
    {
        using var scope = serviceProvider.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<TEntity>().IgnoreQueryFilters().CountAsync(predicate);
    }
}
