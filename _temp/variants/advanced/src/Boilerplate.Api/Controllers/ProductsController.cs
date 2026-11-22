using Boilerplate.Shared.Application;
using Boilerplate.Application.Products;
using Microsoft.AspNetCore.Mvc;

namespace Boilerplate.Api.Controllers;

[ApiController]
[Route("products")]
public class ProductsController(ICommandDispatcher commandDispatcher) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductCommand command)
    {
        var commandResult = await commandDispatcher.Send(command);

        return Created($"products/{commandResult.Content.Id}", commandResult.Content);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get([FromRoute] GetProductCommand command)
    {
        var commandResult = await commandDispatcher.Send(command);

        return Ok(commandResult.Content);
    }

    [HttpPut("{id:guid}/name")]
    public async Task<IActionResult> Rename([FromRoute] Guid id, [FromBody] RenameProductCommand command)
    {
        var commandResult = await commandDispatcher.Send(command with { Id = id });

        return Ok(commandResult.Content);
    }
}
