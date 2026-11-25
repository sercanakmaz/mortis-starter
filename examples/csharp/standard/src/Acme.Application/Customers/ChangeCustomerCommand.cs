using System.Text.Json.Serialization;
using Acme.Domain.Aggregates.Customers;
using Acme.Shared.Application;
using Acme.Shared.Exceptions;
using FluentValidation;

namespace Acme.Application.Customers;

public record ChangeCustomerCommand : ICommand<CustomerDto>, IContextCommand, ITransactionalCommand
{
    [JsonIgnore] public CommandContext Context { get; set; }
    [JsonIgnore] public Guid Id { get; set; }

    public Optional<string> Name { get; set; }
    public Optional<string> Note { get; set; }
}

public class ChangeCustomerCommandValidator : AbstractValidator<ChangeCustomerCommand>
{
    public ChangeCustomerCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.Name.Value).NotEmpty().MaximumLength(CustomerConstants.NameMaxLength).When(command => command.Name.HasValue).OverridePropertyName(nameof(ChangeCustomerCommand.Name));
        RuleFor(command => command.Note.Value).MaximumLength(CustomerConstants.NoteMaxLength).When(command => command.Note.HasValue).OverridePropertyName(nameof(ChangeCustomerCommand.Note));
    }
}

public class ChangeCustomerCommandHandler(
    ICustomerRepository customerRepository,
    CommandPipeline<ChangeCustomerCommand> commandPipeline) : BaseCommandHandler<ChangeCustomerCommand, CustomerDto>(commandPipeline)
{
    protected override async Task<CommandResult<CustomerDto>> OnHandle(ChangeCustomerCommand command)
    {
        var customer = await customerRepository.FindByIdTrackingAsync(command.Id)
            ?? throw new NotFoundException(nameof(Customer));

        customer.Change(command.Name.Or(customer.Name), command.Note.Or(customer.Note));
        customer.SetUpdatedAudit(command.Context.User);

        await customerRepository.UpdateAsync(customer);

        return CommandResult.FromContent(CustomerDto.Map(customer));
    }
}
