using System.Text.Json.Serialization;
using Boilerplate.Domain.Aggregates.Customers;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Exceptions;

namespace Boilerplate.Application.Customers;

public record RegisterCompletedOrderCommand(int Id) : ICommand<CustomerDto>, IContextCommand, ITransactionalCommand
{
    [JsonIgnore] public CommandContext Context { get; set; }
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
        customer.SetUpdatedAudit(command.Context.User);

        await customerRepository.UpdateAsync(customer);

        return CommandResult.FromContent(CustomerDto.Map(customer));
    }
}
