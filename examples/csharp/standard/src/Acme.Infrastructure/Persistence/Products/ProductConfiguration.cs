using Acme.Domain.Aggregates.Categories;
using Acme.Domain.Aggregates.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acme.Infrastructure.Persistence.Products;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.AggregateConfiguration<Product, Guid>();

        builder.Property(product => product.Sku).HasMaxLength(ProductConstants.SkuMaxLength).IsRequired();
        builder.Property(product => product.Name).HasMaxLength(ProductConstants.NameMaxLength).IsRequired();
        builder.Property(product => product.Stock).IsRequired();

        builder.HasAlternateKey(product => product.Sku);

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(product => product.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property<uint>(PersistenceConstants.RowVersionColumn).IsRowVersion();

        builder.ToTable("products", table => table.HasCheckConstraint("ck_products_stock_not_negative", "stock >= 0"));
    }
}
