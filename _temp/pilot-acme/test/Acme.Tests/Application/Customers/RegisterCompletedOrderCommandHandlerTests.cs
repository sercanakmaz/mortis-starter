using Acme.Application.Customers;
using Acme.Domain.Aggregates.Customers;
using Acme.Shared.Exceptions;
using Acme.Shared.Persistence;
using Acme.Tests.TestSupport.Domain.Customers;
using Acme.Tests.TestSupport.Shared.Application;
using Acme.Tests.TestSupport.Shared.Persistence;
using Moq;

namespace Acme.Tests.Application.Customers;

public class RegisterCompletedOrderCommandHandlerTests
{
    [Fact]
    public async Task Handle_Should_IncrementTheCountAndUpdateTheCustomer()
    {
        var calls = new List<string>();
        Customer writtenCustomer = null;
        var customerRepository = CustomerRepositoryBuilder.Create()
            .WithFindByIdTrackingAsync(FakeCustomer.Create().WithCompletedOrderCount(1).WithoutEvents())
            .WithUpdateAsync(updatedCustomer =>
            {
                writtenCustomer = updatedCustomer;
                calls.Add(nameof(ICustomerRepository.UpdateAsync));
            })
            .Build();
        var commandPipeline = CommandPipelineBuilder<RegisterCompletedOrderCommand>.Create()
            .WithUnitOfWork(UnitOfWorkBuilder.Create().WithRecordedCalls(calls).Build())
            .Build();
        var commandHandler = new RegisterCompletedOrderCommandHandler(customerRepository, commandPipeline);

        var commandResult = await commandHandler.Handle(new RegisterCompletedOrderCommand(Guid.CreateVersion7()) { UserId = "user-1", UserEmail = "user-1@localhost" });

        Assert.Equal(2, commandResult.Content.CompletedOrderCount);
        Assert.Equal("user-1", writtenCustomer.UpdatedUserId);
        Assert.Equal([nameof(IUnitOfWork.BeginTransactionAsync), nameof(ICustomerRepository.UpdateAsync), nameof(IUnitOfWork.CommitTransactionAsync)], calls);
    }

    [Fact]
    public async Task Handle_WhenTheCustomerIsBlocked_Should_ThrowBlockedAndNotWrite()
    {
        var customerRepositoryBuilder = CustomerRepositoryBuilder.Create().WithFindByIdTrackingAsync(FakeCustomer.Create().WithIsBlocked(true)).WithUpdateAsync();
        var commandHandler = new RegisterCompletedOrderCommandHandler(customerRepositoryBuilder.Build(), CommandPipelineBuilder<RegisterCompletedOrderCommand>.Create().Build());

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => commandHandler.Handle(new RegisterCompletedOrderCommand(Guid.CreateVersion7())));

        Assert.Equal(CustomerConstants.BlockedCode, exception.Code);
        customerRepositoryBuilder.BuildMock().Verify(customerRepository => customerRepository.UpdateAsync(It.IsAny<Customer>()), Times.Never);
    }
}
