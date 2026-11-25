using System.Linq.Expressions;
using Boilerplate.Application;
using Boilerplate.Domain;
using Boilerplate.Infrastructure;
using Boilerplate.Infrastructure.Persistence;
using Boilerplate.Infrastructure.Messaging.RabbitMq;
using Boilerplate.Infrastructure.Tenancy;
using Boilerplate.Shared;
using Boilerplate.Shared.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerplate.IntegrationTests.TestSupport;

public static class IntegrationServices
{
    public static readonly Guid TenantA = Guid.Parse("0197a0a0-0000-7000-8000-00000000000a");
    public static readonly Guid TenantB = Guid.Parse("0197a0a0-0000-7000-8000-00000000000b");

    public static ServiceProvider Create(string connectionString, string rabbitMqUri = "amqp://guest:guest@localhost:5672")
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                [$"ConnectionStrings:{PersistenceConstants.ConnectionStringName}"] = connectionString,
                [$"ConnectionStrings:{RabbitMqConstants.ConnectionStringName}"] = rabbitMqUri
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSharedModule().AddDomainModule().AddApplicationModule().AddInfrastructureModule(configuration);

        return services.BuildServiceProvider();
    }

    public static async Task<TContent> Send<TContent>(this ServiceProvider serviceProvider, ICommand<TContent> command, Guid tenantId)
    {
        using var scope = serviceProvider.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantScope>().Enter(tenantId.ToString());

        var commandResult = await scope.ServiceProvider.GetRequiredService<ICommandDispatcher>().Send(command);

        return commandResult.Content;
    }

    public static async Task<TResult> Ask<TResult>(this ServiceProvider serviceProvider, IQuery<TResult> query, Guid tenantId)
    {
        using var scope = serviceProvider.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantScope>().Enter(tenantId.ToString());

        return await scope.ServiceProvider.GetRequiredService<IQueryDispatcher>().Ask(query);
    }

    public static async Task<int> CountAsync<TEntity>(this ServiceProvider serviceProvider, Expression<Func<TEntity, bool>> predicate) where TEntity : class
    {
        using var scope = serviceProvider.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<TEntity>().IgnoreQueryFilters().CountAsync(predicate);
    }
}
