using System.Text.Json.Serialization;
using Boilerplate.Domain.Aggregates.Customers;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Exceptions;
using FluentValidation;

namespace Boilerplate.Application.Customers;

public record SetCustomerTaxNumberCommand : ICommand<CustomerDto>, IContextCommand, ITransactionalCommand
{
    [JsonIgnore] public CommandContext Context { get; set; }

    [JsonIgnore] public Guid Id { get; set; }
    public string TaxNumber { get; set; }
}

public class SetCustomerTaxNumberCommandValidator : AbstractValidator<SetCustomerTaxNumberCommand>
{
    public SetCustomerTaxNumberCommandValidator()
    {
        RuleFor(command => command.TaxNumber).NotEmpty().MaximumLength(CustomerConstants.TaxNumberMaxLength);
    }
}

public class SetCustomerTaxNumberCommandHandler(
    ICustomerRepository customerRepository,
    CommandPipeline<SetCustomerTaxNumberCommand> commandPipeline) : BaseCommandHandler<SetCustomerTaxNumberCommand, CustomerDto>(commandPipeline)
{
    protected override async Task<CommandResult<CustomerDto>> OnHandle(SetCustomerTaxNumberCommand command)
    {
        var customer = await customerRepository.FindByIdTrackingAsync(command.Id)
            ?? throw new NotFoundException(nameof(Customer));

        customer.SetTaxNumber(command.TaxNumber);
        customer.SetUpdatedAudit(command.Context.User);

        await customerRepository.UpdateAsync(customer);

        return CommandResult.FromContent(CustomerDto.Map(customer));
    }
}
