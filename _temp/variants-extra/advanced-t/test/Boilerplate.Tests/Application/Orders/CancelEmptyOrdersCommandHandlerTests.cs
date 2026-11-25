using Boilerplate.Application.Orders;
using Boilerplate.Domain.Aggregates.Orders;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Domain;
using Boilerplate.Tests.TestSupport.Domain.Orders;
using Boilerplate.Tests.TestSupport.Shared.Application;

namespace Boilerplate.Tests.Application.Orders;

public class CancelEmptyOrdersCommandHandlerTests
{
    [Fact]
    public async Task Handle_Should_CancelEveryOrderWhoseLinesAreAllCancelledAsTheJobsUser()
    {
        var written = new List<Order>();
        var emptyOrder = FakeOrder.Create().WithLine("SKU-1").WithCancelledLine("SKU-1").WithoutEvents();
        var activeOrder = FakeOrder.Create().WithLine("SKU-2").WithoutEvents();
        var orderRepository = OrderRepositoryBuilder.Create().WithFindOpenWithAllLinesCancelledTrackingAsync(emptyOrder, activeOrder).WithUpdateAsync(written.Add).Build();
        var commandHandler = new CancelEmptyOrdersCommandHandler(orderRepository, CommandPipelineBuilder<CancelEmptyOrdersCommand>.Create().Build());

        var commandResult = await commandHandler.Handle(new CancelEmptyOrdersCommand { Context = new CommandContext { User = new AuditUser("scheduler", "scheduler@localhost") } });

        Assert.Equal(2, commandResult.Content);
        Assert.Equal((OrderStatus.Cancelled, "scheduler"), (Assert.Single(written).Status, emptyOrder.UpdatedUserId));
        Assert.Equal(OrderStatus.Open, activeOrder.Status);
    }
}
