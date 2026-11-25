using Microsoft.AspNetCore.Mvc;
using Acme.Application.Categories;
using Acme.Shared.Application;

namespace Acme.Api.Controllers;

[ApiController]
[Route("categories")]
public class CategoriesController(ICommandDispatcher commandDispatcher) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCategoryCommand command)
    {
        var commandResult = await commandDispatcher.Send(command);

        return Created($"categories/{commandResult.Content.Id}", commandResult.Content);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get([FromRoute] int id)
    {
        var commandResult = await commandDispatcher.Send(new GetCategoryCommand(id));

        return Ok(commandResult.Content);
    }
}
