using Boilerplate.Shared.Domain;

namespace Boilerplate.Infrastructure.ReadModels.OrderSummaries;

public class OrderSummaryRefresh<TEvent>(OrderSummaryProjection orderSummaryProjection, Func<TEvent, string> orderIdOf) : BaseSyncEventHandler<TEvent> where TEvent : EventBase
{
    public override int Sequence => 100;

    protected override Task OnHandle(TEvent @event)
    {
        return orderSummaryProjection.RefreshAsync(Guid.Parse(orderIdOf(@event)));
    }
}
