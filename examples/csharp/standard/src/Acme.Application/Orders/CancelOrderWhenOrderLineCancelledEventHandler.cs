using Acme.Domain.Aggregates.Orders.Events;
using Acme.Shared.Application;
using Acme.Shared.Domain;

namespace Acme.Application.Orders;

public class CancelOrderWhenOrderLineCancelledEventHandler(ICommandDispatcher commandDispatcher) : BaseSyncEventHandler<LineCancelled>
{
    protected override async Task OnHandle(LineCancelled @event)
    {
        await commandDispatcher.Send(new CancelOrderWhenAllLinesCancelledCommand(Guid.Parse(@event.Id)) { Context = ContextOf(@event) });
    }
}
