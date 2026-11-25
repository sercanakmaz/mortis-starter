using Boilerplate.Shared.Application;
using Boilerplate.Application.Customers;
using Microsoft.AspNetCore.Mvc;

namespace Boilerplate.Api.Controllers;

[ApiController]
[Route("customers")]
public class CustomersController(ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCustomerCommand command)
    {
        var commandResult = await commandDispatcher.Send(command);

        return Created($"customers/{commandResult.Content.Id}", commandResult.Content);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get([FromRoute] GetCustomerQuery query)
    {
        return Ok(await queryDispatcher.Ask(query));
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Change([FromRoute] Guid id, [FromBody] ChangeCustomerCommand command)
    {
        var commandResult = await commandDispatcher.Send(command with { Id = id });

        return Ok(commandResult.Content);
    }

    [HttpPut("{id:guid}/block")]
    public async Task<IActionResult> Block([FromRoute] Guid id)
    {
        var commandResult = await commandDispatcher.Send(new BlockCustomerCommand(id));

        return Ok(commandResult.Content);
    }

    [HttpPut("{id:guid}/tax-number")]
    public async Task<IActionResult> SetTaxNumber([FromRoute] Guid id, [FromBody] SetCustomerTaxNumberCommand command)
    {
        var commandResult = await commandDispatcher.Send(command with { Id = id });

        return Ok(commandResult.Content);
    }
}
