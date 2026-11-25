using Boilerplate.Shared.Application;
using Boilerplate.Application.Products;
using Microsoft.AspNetCore.Mvc;

namespace Boilerplate.Api.Controllers;

[ApiController]
[Route("products")]
public class ProductsController(ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductCommand command)
    {
        var commandResult = await commandDispatcher.Send(command);

        return Created($"products/{commandResult.Content.Id}", commandResult.Content);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get([FromRoute] GetProductQuery query)
    {
        return Ok(await queryDispatcher.Ask(query));
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] ListProductsQuery query)
    {
        return Ok(await queryDispatcher.Ask(query));
    }

    [HttpPut("{id:guid}/name")]
    public async Task<IActionResult> Rename([FromRoute] Guid id, [FromBody] RenameProductCommand command)
    {
        var commandResult = await commandDispatcher.Send(command with { Id = id });

        return Ok(commandResult.Content);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete([FromRoute] DeleteProductCommand command)
    {
        await commandDispatcher.Send(command);

        return NoContent();
    }

    [HttpPost("{id:guid}/restore")]
    public async Task<IActionResult> Restore([FromRoute] RestoreProductCommand command)
    {
        var commandResult = await commandDispatcher.Send(command);

        return Ok(commandResult.Content);
    }
}
