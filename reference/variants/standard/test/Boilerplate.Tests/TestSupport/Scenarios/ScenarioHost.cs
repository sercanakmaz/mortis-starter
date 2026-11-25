using Boilerplate.Application;
using Boilerplate.Domain;
using Boilerplate.Shared;
using Boilerplate.Shared.Application;
using Boilerplate.Tests.TestSupport.Shared.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Boilerplate.Tests.TestSupport.Scenarios;

public sealed class ScenarioHost
{
    private readonly ServiceCollection _services = new();

    private ScenarioHost()
    {
        _services.AddLogging();
        _services.AddSharedModule().AddDomainModule().AddApplicationModule();
        _services.AddScoped(_ => UnitOfWorkBuilder.Create().WithHasTransaction(true).Build());

        foreach (var port in typeof(DomainModule).Assembly.GetTypes().Where(type => type.IsInterface && (type.Name.EndsWith("Repository") || type.Name.EndsWith("Reader"))))
        {
            _services.AddScoped(port, _ => ((Mock)Activator.CreateInstance(typeof(Mock<>).MakeGenericType(port))).Object);
        }
    }

    public static ScenarioHost Create() => new();

    public ScenarioHost With<TPort>(TPort port) where TPort : class
    {
        _services.AddScoped(_ => port);

        return this;
    }

    public ICommandDispatcher Dispatcher() => _services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }).CreateScope().ServiceProvider.GetRequiredService<ICommandDispatcher>();
}
