using System.Text.Json.Serialization;
using Acme.Domain.Aggregates.Customers;
using Acme.Shared.Application;
using Acme.Shared.Exceptions;

namespace Acme.Application.Customers;

public record RegisterCompletedOrderCommand(Guid Id) : ICommand<CustomerDto>, IUserCommand, ITransactionalCommand
{
    [JsonIgnore] public string UserId { get; set; }

    [JsonIgnore] public string UserEmail { get; set; }
}

public class RegisterCompletedOrderCommandHandler(
    ICustomerRepository customerRepository,
    CommandPipeline<RegisterCompletedOrderCommand> commandPipeline) : BaseCommandHandler<RegisterCompletedOrderCommand, CustomerDto>(commandPipeline)
{
    protected override async Task<CommandResult<CustomerDto>> OnHandle(RegisterCompletedOrderCommand command)
    {
        var customer = await customerRepository.FindByIdTrackingAsync(command.Id)
            ?? throw new NotFoundException(nameof(Customer));

        customer.RegisterCompletedOrder();
        customer.SetUpdatedAudit(command.ToAuditUser());

        await customerRepository.UpdateAsync(customer);

        return CommandResult.FromContent(CustomerDto.Map(customer));
    }
}
