using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Acme.Domain.Aggregates.Customers;
using Acme.Domain.Aggregates.Orders;
using Acme.Domain.Aggregates.Products;

namespace Acme.Infrastructure.Persistence.Orders;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.AggregateConfiguration<Order, Guid>();

        builder.Property(order => order.OrderNumber).HasMaxLength(OrderConstants.OrderNumberMaxLength).IsRequired();
        builder.Property(order => order.Status).HasConversion<string>().HasMaxLength(32);

        builder.HasIndex(order => order.OrderNumber).IsUnique();

        builder.HasOne<Customer>()
            .WithMany()
            .HasForeignKey(order => order.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(order => order.Lines)
            .WithOne()
            .HasForeignKey(orderLine => orderLine.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(order => order.Lines).AutoInclude();

        builder.ToTable("orders");
    }
}

public class OrderLineConfiguration : IEntityTypeConfiguration<OrderLine>
{
    public void Configure(EntityTypeBuilder<OrderLine> builder)
    {
        builder.ChildEntityConfiguration<OrderLine, Guid>();

        builder.Property(orderLine => orderLine.Sku).HasMaxLength(OrderConstants.SkuMaxLength).IsRequired();
        builder.Property(orderLine => orderLine.CancellationReason).HasMaxLength(OrderConstants.ReasonMaxLength);

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(orderLine => orderLine.Sku)
            .HasPrincipalKey(product => product.Sku)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ComplexProperty(orderLine => orderLine.Price, price =>
        {
            price.IsRequired();
            price.Property(money => money.Amount).HasColumnName("price_amount").HasPrecision(18, 4);
            price.Property(money => money.Currency).HasColumnName("price_currency").HasMaxLength(OrderConstants.CurrencyLength).IsRequired();
        });

        builder.ToTable("order_lines");
    }
}
