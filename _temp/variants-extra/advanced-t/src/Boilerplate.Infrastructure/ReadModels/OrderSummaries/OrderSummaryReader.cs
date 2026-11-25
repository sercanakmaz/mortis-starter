using Microsoft.EntityFrameworkCore;
using Boilerplate.Domain.Aggregates.Orders;
using Boilerplate.Infrastructure.Persistence;
using Boilerplate.Shared.Application;

namespace Boilerplate.Infrastructure.ReadModels.OrderSummaries;

public class OrderSummaryReader(AppDbContext context) : IOrderSummaryReader
{
    public async Task<PagedResult<OrderSummaryView>> ListAsync(int page, int size, string status)
    {
        var summaries = context.Set<OrderSummary>().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
        {
            summaries = summaries.Where(orderSummary => orderSummary.Status == status);
        }

        var total = await summaries.CountAsync();
        var items = await summaries
            .OrderBy(orderSummary => orderSummary.OrderNumber).ThenBy(orderSummary => orderSummary.OrderId)
            .Skip(PageRequest.Skip(page, size)).Take(size)
            .Select(orderSummary => new OrderSummaryView(orderSummary.OrderId, orderSummary.OrderNumber, orderSummary.CustomerName, orderSummary.Status, orderSummary.TotalAmount, orderSummary.LineCount))
            .ToListAsync();

        return new PagedResult<OrderSummaryView>(items, page, size, total);
    }
}
