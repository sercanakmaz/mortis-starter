using Acme.Application.Customers;
using Acme.Domain.Aggregates.Customers;
using Acme.Domain.Aggregates.Orders.Events;
using Acme.Tests.TestSupport.Domain.Customers;
using Acme.Tests.TestSupport.Shared.Application;
using Moq;

namespace Acme.Tests.Application.Customers;

public class RegisterCompletedOrderWhenOrderCompletedEventHandlerTests
{
    [Fact]
    public async Task Handle_Should_RegisterTheCompletedOrderOnTheCustomerOfTheEvent()
    {
        Customer writtenCustomer = null;
        var customer = FakeCustomer.Create();
        var customerRepositoryBuilder = CustomerRepositoryBuilder.Create()
            .WithFindByIdTrackingAsync(customer)
            .WithUpdateAsync(updatedCustomer => writtenCustomer = updatedCustomer);
        var customerRepository = customerRepositoryBuilder.Build();
        var commandHandler = new RegisterCompletedOrderCommandHandler(customerRepository, CommandPipelineBuilder<RegisterCompletedOrderCommand>.Create().Build());
        var eventHandler = new RegisterCompletedOrderWhenOrderCompletedEventHandler(CommandDispatcherBuilder.Create().WithCommandHandler(commandHandler).Build());

        await eventHandler.Handle(new Completed(Guid.CreateVersion7().ToString(), "ORD-1", customer.Id.ToString()));

        customerRepositoryBuilder.BuildMock().Verify(customerRepository => customerRepository.FindByIdTrackingAsync(customer.Id), Times.Once);
        Assert.Equal(1, writtenCustomer.CompletedOrderCount);
    }

    [Fact]
    public async Task Handle_Should_RunTheCommandAsTheUserWhoRaisedTheEvent()
    {
        Customer writtenCustomer = null;
        var customer = FakeCustomer.Create();
        var customerRepositoryBuilder = CustomerRepositoryBuilder.Create()
            .WithFindByIdTrackingAsync(customer)
            .WithUpdateAsync(updatedCustomer => writtenCustomer = updatedCustomer);
        var customerRepository = customerRepositoryBuilder.Build();
        var commandHandler = new RegisterCompletedOrderCommandHandler(customerRepository, CommandPipelineBuilder<RegisterCompletedOrderCommand>.Create().Build());
        var eventHandler = new RegisterCompletedOrderWhenOrderCompletedEventHandler(CommandDispatcherBuilder.Create().WithCommandHandler(commandHandler).Build());

        await eventHandler.Handle(new Completed(Guid.CreateVersion7().ToString(), "ORD-1", customer.Id.ToString()) { UserId = "user-7", UserEmail = "user-7@localhost" });

        Assert.Equal(["user-7", "user-7@localhost"], [writtenCustomer.UpdatedUserId, writtenCustomer.UpdatedUserName]);
    }
}
