using Acme.Application.Customers;
using Acme.Domain.Aggregates.Customers;
using Acme.Domain.Aggregates.Orders.Events;
using Acme.Tests.TestSupport.Domain.Customers;
using Acme.Tests.TestSupport.Shared.Application;

namespace Acme.Tests.Application.Customers;

public class RegisterCompletedOrderWhenOrderCompletedEventHandlerTests
{
    [Fact]
    public async Task Handle_Should_RegisterTheCompletedOrderOnTheCustomerOfTheEvent()
    {
        Customer writtenCustomer = null;
        var customerRepositoryBuilder = CustomerRepositoryBuilder.Create()
            .WithFindByIdTrackingAsync(FakeCustomer.Create().WithCompletedOrderCount(4).WithoutEvents())
            .WithUpdateAsync(updatedCustomer => writtenCustomer = updatedCustomer);
        var commandHandler = new RegisterCompletedOrderCommandHandler(customerRepositoryBuilder.Build(), CommandPipelineBuilder<RegisterCompletedOrderCommand>.Create().Build());
        var eventHandler = new RegisterCompletedOrderWhenOrderCompletedEventHandler(CommandDispatcherBuilder.Create().WithCommandHandler(commandHandler).Build());

        await eventHandler.Handle(new Completed(Guid.CreateVersion7().ToString(), "ORD-1", Guid.CreateVersion7().ToString()));

        Assert.Equal(5, writtenCustomer.CompletedOrderCount);
    }

    [Fact]
    public async Task Handle_Should_RunTheCommandAsTheUserWhoRaisedTheEvent()
    {
        Customer writtenCustomer = null;
        var customerRepositoryBuilder = CustomerRepositoryBuilder.Create()
            .WithFindByIdTrackingAsync(FakeCustomer.Create().WithoutEvents())
            .WithUpdateAsync(updatedCustomer => writtenCustomer = updatedCustomer);
        var commandHandler = new RegisterCompletedOrderCommandHandler(customerRepositoryBuilder.Build(), CommandPipelineBuilder<RegisterCompletedOrderCommand>.Create().Build());
        var eventHandler = new RegisterCompletedOrderWhenOrderCompletedEventHandler(CommandDispatcherBuilder.Create().WithCommandHandler(commandHandler).Build());

        await eventHandler.Handle(new Completed(Guid.CreateVersion7().ToString(), "ORD-1", Guid.CreateVersion7().ToString()) { UserId = "user-7", UserEmail = "user-7@localhost" });

        Assert.Equal("user-7", writtenCustomer.UpdatedUserId);
    }
}
