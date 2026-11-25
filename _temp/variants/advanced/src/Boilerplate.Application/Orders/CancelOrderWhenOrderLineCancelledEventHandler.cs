using Boilerplate.Contracts.Orders;
using Boilerplate.Domain.Aggregates.Orders.Events;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Domain;

namespace Boilerplate.Application.Orders;

public class CancelOrderWhenOrderLineCancelledEventHandler(ICommandDispatcher commandDispatcher) : BaseSyncEventHandler<LineCancelled>
{
    protected override async Task OnHandle(LineCancelled @event)
    {
        await commandDispatcher.Send(new CancelOrderWhenAllLinesCancelledCommand(Guid.Parse(@event.Id)) { Context = ContextOf(@event) });
    }
}
