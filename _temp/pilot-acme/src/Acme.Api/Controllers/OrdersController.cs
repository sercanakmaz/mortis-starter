using Acme.Application.Orders;
using Acme.Shared.Application;
using Microsoft.AspNetCore.Mvc;

namespace Acme.Api.Controllers;

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

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get([FromRoute] Guid id)
    {
        var commandResult = await commandDispatcher.Send(new GetOrderCommand(id));

        return Ok(commandResult.Content);
    }

    [HttpPost("{id:guid}/lines")]
    public async Task<IActionResult> AddLine([FromRoute] Guid id, [FromBody] AddOrderLineCommand command)
    {
        var commandResult = await commandDispatcher.Send(command with { Id = id });

        return Ok(commandResult.Content);
    }

    [HttpPut("{id:guid}/lines/{lineId:guid}/cancel")]
    public async Task<IActionResult> CancelLine([FromRoute] Guid id, [FromRoute] Guid lineId, [FromBody] CancelOrderLineCommand command)
    {
        var commandResult = await commandDispatcher.Send(command with { Id = id, LineId = lineId });

        return Ok(commandResult.Content);
    }

    [HttpPut("{id:guid}/complete")]
    public async Task<IActionResult> Complete([FromRoute] Guid id)
    {
        var commandResult = await commandDispatcher.Send(new CompleteOrderCommand(id));

        return Ok(commandResult.Content);
    }
}
