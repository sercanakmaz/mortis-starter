using Microsoft.AspNetCore.Mvc;
using Acme.Application.Products;
using Acme.Shared.Application;

namespace Acme.Api.Controllers;

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
    public async Task<IActionResult> Get([FromRoute] Guid id)
    {
        var commandResult = await commandDispatcher.Send(new GetProductCommand(id));

        return Ok(commandResult.Content);
    }
}
