using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Boilerplate.Domain.Aggregates.Categories;
using Boilerplate.Domain.Aggregates.Products;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Infrastructure.Persistence.Products;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.AggregateConfiguration<Product, Guid>();

        builder.Property(product => product.Sku).HasMaxLength(ProductConstants.SkuMaxLength).IsRequired();
        builder.Property(product => product.Name).HasConversion(LocalizedTextConversion.Converter, LocalizedTextConversion.Comparer).HasColumnType("jsonb").IsRequired();
        builder.Property(product => product.Stock).IsRequired();

        builder.HasAlternateKey(product => new { product.TenantId, product.Sku });

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(product => product.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property<uint>(PersistenceConstants.RowVersionColumn).IsRowVersion();

        builder.ToTable("products", table => table.HasCheckConstraint("ck_products_stock_not_negative", "stock >= 0"));
    }
}
