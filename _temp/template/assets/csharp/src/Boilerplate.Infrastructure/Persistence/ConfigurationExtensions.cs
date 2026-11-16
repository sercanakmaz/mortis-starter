using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using Boilerplate.Shared.Domain;

namespace Boilerplate.Infrastructure.Persistence;

public static class ConfigurationExtensions
{
    public static EntityTypeBuilder<TAggregate> AggregateConfiguration<TAggregate, TKey>(this EntityTypeBuilder<TAggregate> builder) where TAggregate : AggregateRoot<TKey>
    {
        builder.HasKey(aggregate => aggregate.Id);
        builder.Property(aggregate => aggregate.Id).ValueGeneratedNever();
        builder.Property(aggregate => aggregate.CreatedUserId).HasMaxLength(PersistenceConstants.UserIdMaxLength);
        builder.Property(aggregate => aggregate.CreatedUserName).HasMaxLength(PersistenceConstants.UserNameMaxLength);
        builder.Property(aggregate => aggregate.UpdatedUserId).HasMaxLength(PersistenceConstants.UserIdMaxLength);
        builder.Property(aggregate => aggregate.UpdatedUserName).HasMaxLength(PersistenceConstants.UserNameMaxLength);

        return builder;
    }

    public static EntityTypeBuilder<TEntity> ChildEntityConfiguration<TEntity, TKey>(this EntityTypeBuilder<TEntity> builder) where TEntity : Entity<TKey>
    {
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).ValueGeneratedNever();

        return builder;
    }
}
