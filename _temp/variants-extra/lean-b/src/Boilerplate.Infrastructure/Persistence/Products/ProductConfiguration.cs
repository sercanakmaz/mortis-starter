using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Boilerplate.Domain.Aggregates.Categories;
using Boilerplate.Domain.Aggregates.Products;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Infrastructure.Persistence.Products;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.IdentityAggregateConfiguration<Product, int>();

        builder.Property(product => product.Sku).HasMaxLength(ProductConstants.SkuMaxLength).IsRequired();
        builder.Property(product => product.Name).HasMaxLength(ProductConstants.NameMaxLength).IsRequired();
        builder.Property(product => product.Stock).IsRequired();

        builder.HasAlternateKey(product => product.Sku);

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(product => product.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property<uint>(PersistenceConstants.RowVersionColumn).IsRowVersion();

        builder.ToTable("Products", table => table.HasCheckConstraint("CK_Products_StockNotNegative", "\"Stock\" >= 0"));
    }
}
