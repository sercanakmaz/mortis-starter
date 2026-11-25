using Boilerplate.Contracts.Orders;
using Boilerplate.Domain.Aggregates.Orders.Events;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Domain;

namespace Boilerplate.Application.Products;

public class DecreaseStockWhenOrderLineAddedEventConsumer(ICommandDispatcher commandDispatcher) : BaseEventConsumer<LineAdded>
{
    protected override async Task OnHandle(LineAdded @event)
    {
        await commandDispatcher.Send(new DecreaseProductStockCommand { Sku = @event.Sku, Quantity = @event.Quantity, Context = ContextOf(@event) });
    }
}
