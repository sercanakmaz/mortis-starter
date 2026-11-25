using Acme.Domain.Aggregates.Orders.Events;
using Acme.Shared.Application;
using Acme.Shared.Domain;

namespace Acme.Application.Products;

public class DecreaseStockWhenOrderLineAddedEventConsumer(ICommandDispatcher commandDispatcher) : BaseEventConsumer<LineAdded>
{
    protected override async Task OnHandle(LineAdded @event)
    {
        await commandDispatcher.Send(new DecreaseProductStockCommand { Sku = @event.Sku, Quantity = @event.Quantity, Context = ContextOf(@event) });
    }
}
