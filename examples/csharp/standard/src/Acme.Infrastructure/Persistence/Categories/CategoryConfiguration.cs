using Acme.Domain.Aggregates.Categories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acme.Infrastructure.Persistence.Categories;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.IdentityAggregateConfiguration<Category, int>();

        builder.Property(category => category.Name).HasMaxLength(CategoryConstants.NameMaxLength).IsRequired();

        builder.HasIndex(category => category.Name).IsUnique();

        builder.ToTable("categories");
    }
}
