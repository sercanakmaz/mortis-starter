using System.Text.Json.Serialization;
using Acme.Domain.Aggregates.Customers;
using Acme.Shared.Application;
using FluentValidation;

namespace Acme.Application.Customers;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]
public record CreateCustomerCommand : ICommand<CustomerDto>, IContextCommand, ITransactionalCommand
{
    [JsonIgnore] public CommandContext Context { get; set; }

    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string Name { get; set; }

    public string Email { get; set; }
}

public class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().MaximumLength(CustomerConstants.NameMaxLength);
        RuleFor(command => command.Email).NotEmpty().EmailAddress().MaximumLength(CustomerConstants.EmailMaxLength);
    }
}

public class CreateCustomerCommandHandler(
    ICustomerRepository customerRepository,
    CommandPipeline<CreateCustomerCommand> commandPipeline) : BaseCommandHandler<CreateCustomerCommand, CustomerDto>(commandPipeline)
{
    protected override async Task<CommandResult<CustomerDto>> OnHandle(CreateCustomerCommand command)
    {
        var customer = new Customer(command.Id, command.Name, command.Email, command.Context.User);

        await customerRepository.CreateAsync(customer);

        return CommandResult.FromContent(CustomerDto.Map(customer));
    }
}
