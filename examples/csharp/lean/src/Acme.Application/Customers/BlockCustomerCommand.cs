using System.Text.Json.Serialization;
using Acme.Domain.Aggregates.Customers;
using Acme.Shared.Application;
using Acme.Shared.Exceptions;

namespace Acme.Application.Customers;

public record BlockCustomerCommand(Guid Id) : ICommand<CustomerDto>, IContextCommand, ITransactionalCommand
{
    [JsonIgnore] public CommandContext Context { get; set; }
}

public class BlockCustomerCommandHandler(
    ICustomerRepository customerRepository,
    CommandPipeline<BlockCustomerCommand> commandPipeline) : BaseCommandHandler<BlockCustomerCommand, CustomerDto>(commandPipeline)
{
    protected override async Task<CommandResult<CustomerDto>> OnHandle(BlockCustomerCommand command)
    {
        var customer = await customerRepository.FindByIdTrackingAsync(command.Id)
            ?? throw new NotFoundException(nameof(Customer));

        customer.Block();
        customer.SetUpdatedAudit(command.Context.User);

        await customerRepository.UpdateAsync(customer);

        return CommandResult.FromContent(CustomerDto.Map(customer));
    }
}
