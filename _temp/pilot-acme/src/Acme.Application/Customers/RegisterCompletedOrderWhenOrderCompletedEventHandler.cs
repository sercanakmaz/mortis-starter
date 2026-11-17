using Acme.Domain.Aggregates.Orders.Events;
using Acme.Shared.Application;
using Acme.Shared.Domain;

namespace Acme.Application.Customers;

public class RegisterCompletedOrderWhenOrderCompletedEventHandler(ICommandDispatcher commandDispatcher) : BaseSyncEventHandler<Completed>
{
    protected override async Task OnHandle(Completed @event)
    {
        await commandDispatcher.Send(new RegisterCompletedOrderCommand(Guid.Parse(@event.CustomerId)) { UserId = @event.UserId, UserEmail = @event.UserEmail });
    }
}
