using Boilerplate.Tests.TestSupport.Shared.Application;
using Boilerplate.Tests.TestSupport.Shared.Persistence;
using Boilerplate.Tests.TestSupport.Domain.Customers;
using Boilerplate.Domain.Aggregates.Customers.Events;
using Boilerplate.Domain.Aggregates.Customers;
using Boilerplate.Application.Customers;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Persistence;
using Boilerplate.Shared.Exceptions;
using Boilerplate.Shared.Domain;
using Moq;

namespace Boilerplate.Tests.Application.Customers;

public class BlockCustomerCommandHandlerTests
{
    [Fact]
    public async Task Handle_Should_BlockTheTrackedCustomerAndUpdateIt()
    {
        var calls = new List<string>();
        Customer writtenCustomer = null;
        var customerRepository = CustomerRepositoryBuilder.Create()
            .WithFindByIdTrackingAsync(FakeCustomer.Create().WithoutEvents())
            .WithUpdateAsync(updatedCustomer =>
            {
                writtenCustomer = updatedCustomer;
                calls.Add(nameof(ICustomerRepository.UpdateAsync));
            })
            .Build();
        var commandPipeline = CommandPipelineBuilder<BlockCustomerCommand>.Create()
            .WithUnitOfWork(UnitOfWorkBuilder.Create().WithRecordedCalls(calls).Build())
            .Build();
        var commandHandler = new BlockCustomerCommandHandler(customerRepository, commandPipeline);

        var commandResult = await commandHandler.Handle(new BlockCustomerCommand(1) { Context = new CommandContext { User = new AuditUser("user-1", "user-1@localhost") } });

        Assert.True(commandResult.Content.IsBlocked);
        Assert.Equal("user-1", writtenCustomer.UpdatedUserId);
        Assert.IsType<CustomerBlocked>(Assert.Single(writtenCustomer.DomainEvents));
        Assert.Equal([nameof(IUnitOfWork.BeginTransactionAsync), nameof(ICustomerRepository.UpdateAsync), nameof(IUnitOfWork.CommitTransactionAsync)], calls);
    }

    [Fact]
    public async Task Handle_WithAnUnknownCustomer_Should_ThrowNotFoundAndNotWrite()
    {
        var customerRepositoryBuilder = CustomerRepositoryBuilder.Create().WithFindByIdTrackingAsync(null).WithUpdateAsync();
        var commandHandler = new BlockCustomerCommandHandler(customerRepositoryBuilder.Build(), CommandPipelineBuilder<BlockCustomerCommand>.Create().Build());

        await Assert.ThrowsAsync<NotFoundException>(() => commandHandler.Handle(new BlockCustomerCommand(1)));

        customerRepositoryBuilder.BuildMock().Verify(customerRepository => customerRepository.UpdateAsync(It.IsAny<Customer>()), Times.Never);
    }
}
