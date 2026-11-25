using Microsoft.AspNetCore.Mvc;
using Acme.Application.Customers;
using Acme.Shared.Application;

namespace Acme.Api.Controllers;

[ApiController]
[Route("customers")]
public class CustomersController(ICommandDispatcher commandDispatcher) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCustomerCommand command)
    {
        var commandResult = await commandDispatcher.Send(command);

        return Created($"customers/{commandResult.Content.Id}", commandResult.Content);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get([FromRoute] Guid id)
    {
        var commandResult = await commandDispatcher.Send(new GetCustomerCommand(id));

        return Ok(commandResult.Content);
    }

    [HttpPut("{id:guid}/block")]
    public async Task<IActionResult> Block([FromRoute] Guid id)
    {
        var commandResult = await commandDispatcher.Send(new BlockCustomerCommand(id));

        return Ok(commandResult.Content);
    }
}
