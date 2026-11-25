using Acme.Application.Customers;
using Acme.Domain.Aggregates.Customers;
using Acme.Shared.Application;
using Acme.Shared.Domain;
using Acme.Shared.Exceptions;
using Acme.Tests.TestSupport.Domain.Customers;
using Acme.Tests.TestSupport.Shared.Application;
using Moq;

namespace Acme.Tests.Application.Customers;

public class ChangeCustomerCommandHandlerTests
{
    [Fact]
    public async Task Handle_WithOnlyANullNote_Should_ClearTheNoteAndKeepTheName()
    {
        Customer writtenCustomer = null;
        var customer = FakeCustomer.Create().WithoutEvents();
        customer.Change("Ada", "call before noon");
        var customerRepository = CustomerRepositoryBuilder.Create()
            .WithFindByIdTrackingAsync(customer)
            .WithUpdateAsync(updatedCustomer => writtenCustomer = updatedCustomer)
            .Build();
        var commandHandler = new ChangeCustomerCommandHandler(customerRepository, CommandPipelineBuilder<ChangeCustomerCommand>.Create().Build());

        await commandHandler.Handle(new ChangeCustomerCommand { Id = customer.Id, Note = Optional<string>.Of(null), Context = new CommandContext { User = new AuditUser("user-1", "user-1@localhost") } });

        Assert.Equal("Ada", writtenCustomer.Name);
        Assert.Null(writtenCustomer.Note);
    }

    [Fact]
    public async Task Handle_WithNoMembers_Should_KeepEverything()
    {
        Customer writtenCustomer = null;
        var customer = FakeCustomer.Create().WithoutEvents();
        customer.Change("Ada", "call before noon");
        var customerRepository = CustomerRepositoryBuilder.Create()
            .WithFindByIdTrackingAsync(customer)
            .WithUpdateAsync(updatedCustomer => writtenCustomer = updatedCustomer)
            .Build();
        var commandHandler = new ChangeCustomerCommandHandler(customerRepository, CommandPipelineBuilder<ChangeCustomerCommand>.Create().Build());

        await commandHandler.Handle(new ChangeCustomerCommand { Id = customer.Id, Context = new CommandContext { User = new AuditUser("user-1", "user-1@localhost") } });

        Assert.Equal(("Ada", "call before noon"), (writtenCustomer.Name, writtenCustomer.Note));
    }

    [Fact]
    public async Task Handle_WithAnUnknownCustomer_Should_ThrowNotFoundAndNotWrite()
    {
        var customerRepositoryBuilder = CustomerRepositoryBuilder.Create().WithFindByIdTrackingAsync(null).WithUpdateAsync();
        var commandHandler = new ChangeCustomerCommandHandler(customerRepositoryBuilder.Build(), CommandPipelineBuilder<ChangeCustomerCommand>.Create().Build());

        await Assert.ThrowsAsync<NotFoundException>(() => commandHandler.Handle(new ChangeCustomerCommand { Id = Guid.CreateVersion7() }));

        customerRepositoryBuilder.BuildMock().Verify(customerRepository => customerRepository.UpdateAsync(It.IsAny<Customer>()), Times.Never);
    }

    [Fact]
    public void Validate_WithANullName_Should_FailOnName()
    {
        var validationResult = new ChangeCustomerCommandValidator().Validate(new ChangeCustomerCommand { Id = Guid.CreateVersion7(), Name = Optional<string>.Of(null) });

        Assert.Contains(validationResult.Errors, validationFailure => validationFailure.PropertyName == nameof(ChangeCustomerCommand.Name));
    }
}
