using Boilerplate.Shared.Application;
using Boilerplate.Application.Customers;
using Microsoft.AspNetCore.Mvc;

namespace Boilerplate.Api.Controllers;

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

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get([FromRoute] int id)
    {
        var commandResult = await commandDispatcher.Send(new GetCustomerCommand(id));

        return Ok(commandResult.Content);
    }

    [HttpPut("{id:int}/block")]
    public async Task<IActionResult> Block([FromRoute] int id)
    {
        var commandResult = await commandDispatcher.Send(new BlockCustomerCommand(id));

        return Ok(commandResult.Content);
    }
}
