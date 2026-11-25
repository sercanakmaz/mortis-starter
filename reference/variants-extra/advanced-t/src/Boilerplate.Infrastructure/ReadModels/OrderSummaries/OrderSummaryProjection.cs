using Microsoft.EntityFrameworkCore;
using Boilerplate.Infrastructure.Persistence;

namespace Boilerplate.Infrastructure.ReadModels.OrderSummaries;

public class OrderSummaryProjection(AppDbContext context)
{
    public const int StatusMaxLength = 32;

    public Task RefreshAsync(Guid orderId)
    {
        return context.Database.ExecuteSqlRawAsync(Upsert("where o.id = {0}"), orderId);
    }

    public async Task RebuildAsync()
    {
        await context.Database.ExecuteSqlRawAsync("delete from order_summaries");
        await context.Database.ExecuteSqlRawAsync(Upsert(""));
    }

    private static string Upsert(string condition)
    {
        return $"""
            insert into order_summaries (order_id, tenant_id, order_number, customer_name, status, total_amount, line_count)
            select o.id, o.tenant_id, o.order_number, c.name, o.status,
                   coalesce(sum(l.price_amount * l.quantity) filter (where not l.is_cancelled), 0),
                   count(l.id) filter (where not l.is_cancelled)
            from orders o
            join customers c on c.id = o.customer_id
            left join order_lines l on l.order_id = o.id
            {condition}
            group by o.id, c.name
            on conflict (order_id) do update set
                tenant_id = excluded.tenant_id, order_number = excluded.order_number, customer_name = excluded.customer_name, status = excluded.status,
                total_amount = excluded.total_amount, line_count = excluded.line_count
            """;
    }
}
