using Acme.Domain.Aggregates.Customers;
using Acme.Shared.Application;
using Acme.Shared.Exceptions;

namespace Acme.Application.Customers;

public record GetCustomerCommand(Guid Id) : ICommand<CustomerDto>;

public class GetCustomerCommandHandler(
    ICustomerRepository customerRepository,
    CommandPipeline<GetCustomerCommand> commandPipeline) : BaseCommandHandler<GetCustomerCommand, CustomerDto>(commandPipeline)
{
    protected override async Task<CommandResult<CustomerDto>> OnHandle(GetCustomerCommand command)
    {
        var customer = await customerRepository.FindByIdAsync(command.Id)
            ?? throw new NotFoundException(nameof(Customer));

        return CommandResult.FromContent(CustomerDto.Map(customer));
    }
}
