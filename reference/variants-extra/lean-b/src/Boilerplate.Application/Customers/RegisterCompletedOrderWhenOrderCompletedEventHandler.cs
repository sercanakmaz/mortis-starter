using Boilerplate.Domain.Aggregates.Orders.Events;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Domain;

namespace Boilerplate.Application.Customers;

public class RegisterCompletedOrderWhenOrderCompletedEventHandler(ICommandDispatcher commandDispatcher) : BaseSyncEventHandler<OrderCompleted>
{
    protected override async Task OnHandle(OrderCompleted @event)
    {
        await commandDispatcher.Send(new RegisterCompletedOrderCommand(int.Parse(@event.CustomerId)) { Context = ContextOf(@event) });
    }
}
