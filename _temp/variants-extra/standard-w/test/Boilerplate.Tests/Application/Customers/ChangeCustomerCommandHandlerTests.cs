using Boilerplate.Tests.TestSupport.Shared.Application;
using Boilerplate.Tests.TestSupport.Domain.Customers;
using Boilerplate.Domain.Aggregates.Customers;
using Boilerplate.Application.Customers;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Exceptions;
using Boilerplate.Shared.Domain;

namespace Boilerplate.Tests.Application.Customers;

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
        var commandHandler = new ChangeCustomerCommandHandler(customerRepository, CommandPipelineBuilder<ChangeCustomerCommand>.Create().WithValidator(new ChangeCustomerCommandValidator()).Build());

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
        var commandHandler = new ChangeCustomerCommandHandler(customerRepository, CommandPipelineBuilder<ChangeCustomerCommand>.Create().WithValidator(new ChangeCustomerCommandValidator()).Build());

        await commandHandler.Handle(new ChangeCustomerCommand { Id = customer.Id, Context = new CommandContext { User = new AuditUser("user-1", "user-1@localhost") } });

        Assert.Equal(("Ada", "call before noon"), (writtenCustomer.Name, writtenCustomer.Note));
    }

    [Fact]
    public async Task Handle_WithANullName_Should_FailValidationOnName()
    {
        var customerRepository = CustomerRepositoryBuilder.Create().WithFindByIdTrackingAsync(FakeCustomer.Create().WithoutEvents()).Build();
        var commandHandler = new ChangeCustomerCommandHandler(customerRepository, CommandPipelineBuilder<ChangeCustomerCommand>.Create().WithValidator(new ChangeCustomerCommandValidator()).Build());

        var exception = await Assert.ThrowsAsync<ValidationFailedException>(() => commandHandler.Handle(new ChangeCustomerCommand { Id = Guid.CreateVersion7(), Name = Optional<string>.Of(null) }));

        Assert.Contains(nameof(ChangeCustomerCommand.Name), exception.Errors.Keys);
    }

    [Fact]
    public async Task Handle_WithAnUnknownCustomer_Should_ThrowNotFound()
    {
        var customerRepository = CustomerRepositoryBuilder.Create().WithFindByIdTrackingAsync(null).Build();
        var commandHandler = new ChangeCustomerCommandHandler(customerRepository, CommandPipelineBuilder<ChangeCustomerCommand>.Create().Build());

        await Assert.ThrowsAsync<NotFoundException>(() => commandHandler.Handle(new ChangeCustomerCommand { Id = Guid.CreateVersion7(), Context = new CommandContext() }));
    }
}
