using Acme.Domain.Aggregates.Orders.Events;
using Acme.Shared.Application;
using Acme.Shared.Domain;

namespace Acme.Application.Products;

public class IncreaseStockWhenOrderLineCancelledEventHandler(ICommandDispatcher commandDispatcher) : BaseAsyncEventHandler<LineCancelled>
{
    protected override async Task OnHandle(LineCancelled @event)
    {
        await commandDispatcher.Send(new IncreaseProductStockCommand { Sku = @event.Sku, Quantity = @event.Quantity, UserId = SystemUser.Id, UserEmail = SystemUser.Email });
    }
}
