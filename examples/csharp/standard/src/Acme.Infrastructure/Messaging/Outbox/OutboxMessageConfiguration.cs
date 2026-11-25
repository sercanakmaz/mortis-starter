using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Acme.Infrastructure.Messaging.Outbox;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.HasKey(message => message.Id);
        builder.Property(message => message.Id).ValueGeneratedNever();
        builder.Property(message => message.Consumer).HasMaxLength(300).IsRequired();
        builder.Property(message => message.Type).HasMaxLength(300).IsRequired();
        builder.Property(message => message.Payload).HasColumnType("jsonb").IsRequired();

        builder.HasIndex(message => message.NextAttemptAt);
        builder.HasIndex(message => message.EventId);

        builder.ToTable("outbox_messages");
    }
}
