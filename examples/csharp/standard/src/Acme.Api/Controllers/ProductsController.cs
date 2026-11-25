using Acme.Application.Products;
using Acme.Shared.Application;
using Microsoft.AspNetCore.Mvc;

namespace Acme.Api.Controllers;

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
}
