using Boilerplate.Application;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerplate.Tests.Architecture;

public class RegistrationRulesTests
{
    [Fact]
    public void EveryCommand_Should_HaveARegisteredHandler()
    {
        var registered = new ServiceCollection().AddApplicationModule().Select(descriptor => descriptor.ServiceType).ToHashSet();

        var missing = typeof(ApplicationModule).Assembly.GetTypes()
            .Where(type => !type.IsAbstract)
            .SelectMany(type => type.GetInterfaces().Where(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(ICommand<>)), (type, contract) => (type, content: contract.GetGenericArguments()[0]))
            .Where(command => !registered.Contains(typeof(BaseCommandHandler<,>).MakeGenericType(command.type, command.content)))
            .Select(command => command.type.Name);

        Assert.Empty(missing);
    }

    [Fact]
    public void EveryQuery_Should_HaveARegisteredHandler()
    {
        var registered = new ServiceCollection().AddApplicationModule().Select(descriptor => descriptor.ServiceType).ToHashSet();

        var missing = typeof(ApplicationModule).Assembly.GetTypes()
            .Where(type => !type.IsAbstract)
            .SelectMany(type => type.GetInterfaces().Where(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IQuery<>)), (type, contract) => (type, result: contract.GetGenericArguments()[0]))
            .Where(query => !registered.Contains(typeof(BaseQueryHandler<,>).MakeGenericType(query.type, query.result)))
            .Select(query => query.type.Name);

        Assert.Empty(missing);
    }

    [Fact]
    public void EverySyncEventHandler_Should_BeRegistered()
    {
        var registered = new ServiceCollection().AddApplicationModule().Select(descriptor => descriptor.ImplementationType).ToHashSet();

        var missing = typeof(ApplicationModule).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(ISyncEventHandler).IsAssignableFrom(type) && !registered.Contains(type))
            .Select(type => type.Name);

        Assert.Empty(missing);
    }
}
