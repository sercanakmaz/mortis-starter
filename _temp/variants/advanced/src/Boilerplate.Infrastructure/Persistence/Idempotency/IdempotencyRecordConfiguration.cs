using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Boilerplate.Infrastructure.Persistence.Idempotency;

public class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable(IdempotencyConstants.Table);
        builder.HasKey(record => new { record.Scope, record.Key });
        builder.Property(record => record.Scope).HasMaxLength(IdempotencyConstants.ScopeMaxLength);
        builder.Property(record => record.Key).HasMaxLength(IdempotencyConstants.KeyMaxLength);
        builder.Property(record => record.RequestHash).HasMaxLength(IdempotencyConstants.HashLength).IsRequired();
        builder.HasIndex(record => record.StartedAt);
    }
}
