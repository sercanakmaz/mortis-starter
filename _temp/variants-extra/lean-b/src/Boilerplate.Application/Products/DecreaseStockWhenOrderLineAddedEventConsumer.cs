using Boilerplate.Domain.Aggregates.Orders.Events;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Domain;

namespace Boilerplate.Application.Products;

public class DecreaseStockWhenOrderLineAddedEventConsumer(ICommandDispatcher commandDispatcher) : BaseEventConsumer<OrderLineAdded>
{
    protected override async Task OnHandle(OrderLineAdded @event)
    {
        await commandDispatcher.Send(new DecreaseProductStockCommand { Sku = @event.Sku, Quantity = @event.Quantity, Context = ContextOf(@event) });
    }
}
