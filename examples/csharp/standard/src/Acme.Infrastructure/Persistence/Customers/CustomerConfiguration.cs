using Acme.Domain.Aggregates.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acme.Infrastructure.Persistence.Customers;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.AggregateConfiguration<Customer, Guid>();

        builder.Property(customer => customer.Name).HasMaxLength(CustomerConstants.NameMaxLength).IsRequired();
        builder.Property(customer => customer.Email).HasMaxLength(CustomerConstants.EmailMaxLength).IsRequired();
        builder.Property(customer => customer.Note).HasMaxLength(CustomerConstants.NoteMaxLength);

        builder.HasIndex(customer => customer.Email).IsUnique();

        builder.ToTable("customers");
    }
}
