using Boilerplate.Shared.Application;
using Boilerplate.Application.Orders;
using Microsoft.AspNetCore.Mvc;

namespace Boilerplate.Api.Controllers;

[ApiController]
[Route("orders")]
public class OrdersController(ICommandDispatcher commandDispatcher) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderCommand command)
    {
        var commandResult = await commandDispatcher.Send(command);

        return Created($"orders/{commandResult.Content.Id}", commandResult.Content);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get([FromRoute] int id)
    {
        var commandResult = await commandDispatcher.Send(new GetOrderCommand(id));

        return Ok(commandResult.Content);
    }

    [HttpPost("{id:int}/lines")]
    public async Task<IActionResult> AddLine([FromRoute] int id, [FromBody] AddOrderLineCommand command)
    {
        var commandResult = await commandDispatcher.Send(command with { Id = id });

        return Ok(commandResult.Content);
    }

    [HttpPut("{id:int}/lines/{lineId:guid}/cancel")]
    public async Task<IActionResult> CancelLine([FromRoute] int id, [FromRoute] Guid lineId, [FromBody] CancelOrderLineCommand command)
    {
        var commandResult = await commandDispatcher.Send(command with { Id = id, LineId = lineId });

        return Ok(commandResult.Content);
    }

    [HttpPut("{id:int}/complete")]
    public async Task<IActionResult> Complete([FromRoute] int id)
    {
        var commandResult = await commandDispatcher.Send(new CompleteOrderCommand(id));

        return Ok(commandResult.Content);
    }
}
