using Boilerplate.Infrastructure.Secrets;
using Boilerplate.Infrastructure.Tenancy;
using Boilerplate.Shared.Application;
using Microsoft.EntityFrameworkCore;
using Boilerplate.Shared.Exceptions;
using Boilerplate.Shared.Domain;
using System.Reflection;

namespace Boilerplate.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options, TenantScope tenantScope, ISecretProtector secretProtector) : DbContext(options)
{
    private static readonly MethodInfo FilterByTenantMethod = typeof(AppDbContext).GetMethod(nameof(FilterByTenant), BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly MethodInfo FilterDeletedMethod = typeof(AppDbContext).GetMethod(nameof(FilterDeleted), BindingFlags.NonPublic | BindingFlags.Static);

    private bool TenantBypassed => tenantScope.Bypassed;
    private Guid CurrentTenantId => tenantScope.Required(PersistenceConstants.TenantOwnedData);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        modelBuilder.ProtectSecrets(secretProtector);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(entityType => typeof(IMultiTenant).IsAssignableFrom(entityType.ClrType)).ToList())
        {
            FilterByTenantMethod.MakeGenericMethod(entityType.ClrType).Invoke(this, [modelBuilder]);
        }

        foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(entityType => typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType)).ToList())
        {
            FilterDeletedMethod.MakeGenericMethod(entityType.ClrType).Invoke(null, [modelBuilder]);
        }
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<IMultiTenant>().ToList())
        {
            var tenantId = tenantScope.Required(entry.Metadata.ClrType.Name);

            if (entry.State == EntityState.Added && entry.Entity.TenantId == Guid.Empty)
            {
                entry.Property(nameof(IMultiTenant.TenantId)).CurrentValue = tenantId;
            }
            else if (entry.State != EntityState.Unchanged && entry.Entity.TenantId != tenantId && !tenantScope.Bypassed)
            {
                throw new ForbiddenException($"Writing another tenant's {entry.Metadata.ClrType.Name}");
            }
        }

        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void FilterByTenant<TEntity>(ModelBuilder modelBuilder) where TEntity : class, IMultiTenant
    {
        modelBuilder.Entity<TEntity>().Property(entity => entity.TenantId).IsRequired();
        modelBuilder.Entity<TEntity>().HasQueryFilter(PersistenceConstants.TenantFilter, entity => TenantBypassed || entity.TenantId == CurrentTenantId);
    }

    private static void FilterDeleted<TEntity>(ModelBuilder modelBuilder) where TEntity : class, ISoftDeletable
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(PersistenceConstants.SoftDeleteFilter, entity => !entity.IsDeleted);
        modelBuilder.Entity<TEntity>().HasIndex(entity => entity.IsDeleted);
    }
}
