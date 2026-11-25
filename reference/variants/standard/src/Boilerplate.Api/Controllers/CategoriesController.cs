using Boilerplate.Application.Categories;
using Boilerplate.Shared.Application;
using Microsoft.AspNetCore.Mvc;

namespace Boilerplate.Api.Controllers;

[ApiController]
[Route("categories")]
public class CategoriesController(ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCategoryCommand command)
    {
        var commandResult = await commandDispatcher.Send(command);

        return Created($"categories/{commandResult.Content.Id}", commandResult.Content);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get([FromRoute] GetCategoryQuery query)
    {
        return Ok(await queryDispatcher.Ask(query));
    }
}
