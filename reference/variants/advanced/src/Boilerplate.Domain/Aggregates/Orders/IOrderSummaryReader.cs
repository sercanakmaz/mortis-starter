using Boilerplate.Shared.Application;

namespace Boilerplate.Domain.Aggregates.Orders;

public interface IOrderSummaryReader
{
    Task<PagedResult<OrderSummaryView>> ListAsync(int page, int size, string status);
}
