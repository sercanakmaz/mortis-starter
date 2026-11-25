using Acme.Domain.Aggregates.Orders.Events;
using Acme.Shared.Application;
using Acme.Shared.Domain;

namespace Acme.Application.Products;

public class IncreaseStockWhenOrderLineCancelledEventConsumer(ICommandDispatcher commandDispatcher) : BaseEventConsumer<LineCancelled>
{
    protected override async Task OnHandle(LineCancelled @event)
    {
        await commandDispatcher.Send(new IncreaseProductStockCommand { Sku = @event.Sku, Quantity = @event.Quantity, Context = ContextOf(@event) });
    }
}
