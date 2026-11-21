using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Infrastructure.Messaging.Outbox;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Id).ValueGeneratedNever();
        builder.Property(message => message.TenantId).HasMaxLength(OutboxSettings.TenantIdMaxLength);
        builder.Property(message => message.Type).HasMaxLength(300).IsRequired();
        builder.Property(message => message.Payload).HasColumnType("jsonb").IsRequired();

        builder.HasIndex(message => message.NextAttemptAt);

        builder.ToTable("outbox_messages");
    }
}
