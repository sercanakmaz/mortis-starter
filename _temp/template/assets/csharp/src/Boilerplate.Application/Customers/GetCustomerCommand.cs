using Boilerplate.Domain.Aggregates.Customers;
using Boilerplate.Shared.Application;

namespace Boilerplate.Application.Customers;

public record GetCustomerCommand(Guid Id) : ICommand<CustomerDto>;

public class GetCustomerCommandHandler(
    ICustomerService customerService,
    CommandPipeline<GetCustomerCommand> commandPipeline) : BaseCommandHandler<GetCustomerCommand, CustomerDto>(commandPipeline)
{
    protected override async Task<CommandResult<CustomerDto>> OnHandle(GetCustomerCommand command)
    {
        var customer = await customerService.GetAsync(command.Id);

        return CommandResult.FromContent(CustomerDto.Map(customer));
    }
}
