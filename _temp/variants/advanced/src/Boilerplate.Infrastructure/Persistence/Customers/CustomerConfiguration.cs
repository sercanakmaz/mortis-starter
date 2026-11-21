using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Boilerplate.Domain.Aggregates.Customers;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Infrastructure.Persistence.Customers;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.AggregateConfiguration<Customer, Guid>();

        builder.Property(customer => customer.Name).HasMaxLength(CustomerConstants.NameMaxLength).IsRequired();
        builder.Property(customer => customer.Email).HasMaxLength(CustomerConstants.EmailMaxLength).IsRequired();

        builder.HasIndex(customer => new { customer.TenantId, customer.Email }).IsUnique();
        builder.HasAlternateKey(customer => new { customer.TenantId, customer.Id });

        builder.ToTable("customers");
    }
}
