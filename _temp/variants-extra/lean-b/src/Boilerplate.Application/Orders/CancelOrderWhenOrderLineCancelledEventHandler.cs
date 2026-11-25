using Boilerplate.Domain.Aggregates.Orders.Events;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Domain;

namespace Boilerplate.Application.Orders;

public class CancelOrderWhenOrderLineCancelledEventHandler(ICommandDispatcher commandDispatcher) : BaseSyncEventHandler<OrderLineCancelled>
{
    protected override async Task OnHandle(OrderLineCancelled @event)
    {
        await commandDispatcher.Send(new CancelOrderWhenAllLinesCancelledCommand(int.Parse(@event.Id)) { Context = ContextOf(@event) });
    }
}
