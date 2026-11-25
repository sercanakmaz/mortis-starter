using Boilerplate.Application.Customers;
using Boilerplate.Domain.Aggregates.Customers.Events;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Domain;
using Boilerplate.Shared.Exceptions;
using Boilerplate.Tests.TestSupport.Domain.Customers;
using Boilerplate.Tests.TestSupport.Shared.Application;

namespace Boilerplate.Tests.Application.Customers;

public class SetCustomerTaxNumberCommandHandlerTests
{
    [Fact]
    public async Task Handle_Should_SetTheTaxNumberAndRaiseAnEventWithoutIt()
    {
        var customer = FakeCustomer.Create();
        customer.ClearEvents();
        var commandHandler = new SetCustomerTaxNumberCommandHandler(CustomerRepositoryBuilder.Create().WithFindByIdTrackingAsync(customer).WithUpdateAsync().Build(), CommandPipelineBuilder<SetCustomerTaxNumberCommand>.Create().WithValidator(new SetCustomerTaxNumberCommandValidator()).Build());

        var commandResult = await commandHandler.Handle(new SetCustomerTaxNumberCommand { Id = customer.Id, TaxNumber = "TR-123", Context = new CommandContext { User = new AuditUser("user-1", "user-1@localhost") } });

        Assert.Equal("TR-123", commandResult.Content.TaxNumber);
        Assert.Equal(customer.Id.ToString(), Assert.IsType<TaxNumberSet>(Assert.Single(customer.DomainEvents)).Id);
    }

    [Fact]
    public async Task Handle_WithoutATaxNumber_Should_FailValidation()
    {
        var commandHandler = new SetCustomerTaxNumberCommandHandler(CustomerRepositoryBuilder.Create().Build(), CommandPipelineBuilder<SetCustomerTaxNumberCommand>.Create().WithValidator(new SetCustomerTaxNumberCommandValidator()).Build());

        await Assert.ThrowsAsync<ValidationFailedException>(() => commandHandler.Handle(new SetCustomerTaxNumberCommand { Id = Guid.CreateVersion7() }));
    }
}
