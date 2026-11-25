using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Boilerplate.Domain.Aggregates.Customers;
using Boilerplate.Domain.Aggregates.Orders;

namespace Boilerplate.Infrastructure.ReadModels.OrderSummaries;

public class OrderSummaryConfiguration : IEntityTypeConfiguration<OrderSummary>
{
    public void Configure(EntityTypeBuilder<OrderSummary> builder)
    {
        builder.ToTable("order_summaries");
        builder.HasKey(orderSummary => orderSummary.OrderId);
        builder.Property(orderSummary => orderSummary.OrderNumber).HasMaxLength(OrderConstants.OrderNumberMaxLength).IsRequired();
        builder.Property(orderSummary => orderSummary.CustomerName).HasMaxLength(CustomerConstants.NameMaxLength).IsRequired();
        builder.Property(orderSummary => orderSummary.Status).HasMaxLength(OrderSummaryProjection.StatusMaxLength).IsRequired();
        builder.Property(orderSummary => orderSummary.TotalAmount).HasPrecision(18, 4);
        builder.HasIndex(orderSummary => new { orderSummary.TenantId, orderSummary.Status });
    }
}
