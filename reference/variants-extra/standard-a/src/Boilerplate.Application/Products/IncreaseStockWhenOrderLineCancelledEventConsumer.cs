using Boilerplate.Domain.Aggregates.Orders.Events;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Domain;

namespace Boilerplate.Application.Products;

public class IncreaseStockWhenOrderLineCancelledEventConsumer(ICommandDispatcher commandDispatcher) : BaseEventConsumer<LineCancelled>
{
    protected override async Task OnHandle(LineCancelled @event)
    {
        await commandDispatcher.Send(new IncreaseProductStockCommand { Sku = @event.Sku, Quantity = @event.Quantity, Context = ContextOf(@event) });
    }
}
