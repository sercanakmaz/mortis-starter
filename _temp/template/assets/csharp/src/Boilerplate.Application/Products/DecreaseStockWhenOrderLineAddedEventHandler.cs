using Boilerplate.Domain.Aggregates.Orders.Events;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Domain;

namespace Boilerplate.Application.Products;

public class DecreaseStockWhenOrderLineAddedEventHandler(ICommandDispatcher commandDispatcher) : BaseAsyncEventHandler<LineAdded>
{
    protected override async Task OnHandle(LineAdded @event)
    {
        await commandDispatcher.Send(new DecreaseProductStockCommand { Sku = @event.Sku, Quantity = @event.Quantity, UserId = SystemUser.Id, UserEmail = SystemUser.Email });
    }
}
