using System.Text.Json.Serialization;
using Boilerplate.Domain.Aggregates.Customers;
using Boilerplate.Shared.Application;

namespace Boilerplate.Application.Customers;

public record BlockCustomerCommand(Guid Id) : ICommand<CustomerDto>, IUserCommand, ITransactionalCommand
{
    [JsonIgnore] public string UserId { get; set; }
    [JsonIgnore] public string UserEmail { get; set; }
}

public class BlockCustomerCommandHandler(
    ICustomerService customerService,
    CommandPipeline<BlockCustomerCommand> commandPipeline) : BaseCommandHandler<BlockCustomerCommand, CustomerDto>(commandPipeline)
{
    protected override async Task<CommandResult<CustomerDto>> OnHandle(BlockCustomerCommand command)
    {
        var customer = await customerService.BlockAsync(command.ToAuditUser(), command.Id);

        return CommandResult.FromContent(CustomerDto.Map(customer));
    }
}
