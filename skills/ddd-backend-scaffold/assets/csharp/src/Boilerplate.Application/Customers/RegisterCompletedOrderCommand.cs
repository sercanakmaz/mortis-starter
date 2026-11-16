using System.Text.Json.Serialization;
using Boilerplate.Domain.Aggregates.Customers;
using Boilerplate.Shared.Application;

namespace Boilerplate.Application.Customers;

public record RegisterCompletedOrderCommand(Guid Id) : ICommand<CustomerDto>, IUserCommand, ITransactionalCommand
{
    [JsonIgnore] public string UserId { get; set; }
    [JsonIgnore] public string UserEmail { get; set; }
}

public class RegisterCompletedOrderCommandHandler(
    ICustomerService customerService,
    CommandPipeline<RegisterCompletedOrderCommand> commandPipeline) : BaseCommandHandler<RegisterCompletedOrderCommand, CustomerDto>(commandPipeline)
{
    protected override async Task<CommandResult<CustomerDto>> OnHandle(RegisterCompletedOrderCommand command)
    {
        var customer = await customerService.RegisterCompletedOrderAsync(command.ToAuditUser(), command.Id);

        return CommandResult.FromContent(CustomerDto.Map(customer));
    }
}
