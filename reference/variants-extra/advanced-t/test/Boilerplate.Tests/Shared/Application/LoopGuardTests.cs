using Boilerplate.Shared;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Domain;
using Boilerplate.Shared.Exceptions;
using Boilerplate.Shared.Persistence;
using Boilerplate.Tests.TestSupport.Shared.Application;
using Boilerplate.Tests.TestSupport.Shared.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerplate.Tests.Shared.Application;

public class LoopGuardTests
{
    [Fact]
    public async Task Send_WithTwoHandlersThatTriggerEachOther_Should_StopAtTheLimitAndNameTheChain()
    {
        var services = new ServiceCollection().AddSharedModule();
        services.AddSingleton(UnitOfWorkBuilder.Create().Build());
        services.AddSingleton<PingPongLog>();
        services.AddCommandHandler<PingCommand, int, PingCommandHandler>();
        services.AddCommandHandler<PongCommand, int, PongCommandHandler>();
        services.AddScoped<BaseSyncEventHandler<Pinged>, SendPongWhenPinged>();
        services.AddScoped<BaseSyncEventHandler<Ponged>, SendPingWhenPonged>();
        await using var serviceProvider = services.BuildServiceProvider();
        using var scope = serviceProvider.CreateScope();

        var exception = await Assert.ThrowsAsync<LoopDetectedException>(() => scope.ServiceProvider.GetRequiredService<ICommandDispatcher>().Send(new PingCommand()));

        var chain = ((string)exception.Parameters["chain"]).Split(LoopGuardConstants.Separator);
        Assert.Equal(LoopGuardConstants.MaxChain + 1, chain.Length);
        Assert.All(chain.Select((name, index) => (name, index)), link => Assert.Equal(link.index % 2 == 0 ? nameof(PingCommand) : nameof(PongCommand), link.name));
        Assert.Equal(LoopGuardConstants.MaxChain, serviceProvider.GetRequiredService<PingPongLog>().Handled.Count);
    }
}
