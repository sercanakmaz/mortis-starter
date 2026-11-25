using Boilerplate.Contracts.Orders;
using Boilerplate.Domain.Aggregates.Orders.Events;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Domain;

namespace Boilerplate.Application.Customers;

public class RegisterCompletedOrderWhenOrderCompletedEventHandler(ICommandDispatcher commandDispatcher) : BaseSyncEventHandler<Completed>
{
    protected override async Task OnHandle(Completed @event)
    {
        await commandDispatcher.Send(new RegisterCompletedOrderCommand(Guid.Parse(@event.CustomerId)) { Context = ContextOf(@event) });
    }
}
