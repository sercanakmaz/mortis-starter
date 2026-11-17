using Acme.Application.Customers;
using Acme.Domain.Aggregates.Customers;
using Acme.Shared.Exceptions;
using Acme.Shared.Persistence;
using Acme.Tests.TestSupport.Domain.Customers;
using Acme.Tests.TestSupport.Shared.Application;
using Acme.Tests.TestSupport.Shared.Persistence;
using Moq;

namespace Acme.Tests.Application.Customers;

public class CreateCustomerCommandHandlerTests
{
    [Fact]
    public async Task Handle_Should_ReturnTheCreatedCustomerWithANormalizedEmailInsideATransaction()
    {
        var calls = new List<string>();
        var customerRepository = CustomerRepositoryBuilder.Create().WithCreateAsync(_ => calls.Add(nameof(ICustomerRepository.CreateAsync))).Build();
        var commandPipeline = CommandPipelineBuilder<CreateCustomerCommand>.Create()
            .WithValidator(new CreateCustomerCommandValidator())
            .WithUnitOfWork(UnitOfWorkBuilder.Create().WithRecordedCalls(calls).Build())
            .Build();
        var commandHandler = new CreateCustomerCommandHandler(customerRepository, commandPipeline);

        var commandResult = await commandHandler.Handle(new CreateCustomerCommand { Name = "Ada", Email = "  Ada@Example.COM " });

        Assert.Equal("ada@example.com", commandResult.Content.Email);
        Assert.Equal("Ada", commandResult.Content.Name);
        Assert.Equal([nameof(IUnitOfWork.BeginTransactionAsync), nameof(ICustomerRepository.CreateAsync), nameof(IUnitOfWork.CommitTransactionAsync)], calls);
    }

    [Fact]
    public async Task Handle_WithAnInvalidEmail_Should_ThrowValidationFailedForEmailAndNotWrite()
    {
        var customerRepositoryBuilder = CustomerRepositoryBuilder.Create().WithCreateAsync();
        var commandPipeline = CommandPipelineBuilder<CreateCustomerCommand>.Create().WithValidator(new CreateCustomerCommandValidator()).Build();
        var commandHandler = new CreateCustomerCommandHandler(customerRepositoryBuilder.Build(), commandPipeline);

        var exception = await Assert.ThrowsAsync<ValidationFailedException>(() => commandHandler.Handle(new CreateCustomerCommand { Name = "Ada", Email = "not-an-email" }));

        Assert.Equal([nameof(CreateCustomerCommand.Email)], exception.Errors.Keys);
        customerRepositoryBuilder.BuildMock().Verify(customerRepository => customerRepository.CreateAsync(It.IsAny<Customer>()), Times.Never);
    }
}
