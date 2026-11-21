using Boilerplate.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Boilerplate.Shared.Exceptions;
using Boilerplate.Shared.Domain;
using System.Reflection;

namespace Boilerplate.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options, TenantScope tenantScope) : DbContext(options)
{
    private static readonly MethodInfo FilterByTenantMethod = typeof(AppDbContext).GetMethod(nameof(FilterByTenant), BindingFlags.NonPublic | BindingFlags.Instance);

    private bool TenantBypassed => tenantScope.Bypassed;
    private Guid CurrentTenantId => tenantScope.Required(PersistenceConstants.TenantOwnedData);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(entityType => typeof(IMultiTenant).IsAssignableFrom(entityType.ClrType)).ToList())
        {
            FilterByTenantMethod.MakeGenericMethod(entityType.ClrType).Invoke(this, [modelBuilder]);
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
        modelBuilder.Entity<TEntity>().HasQueryFilter(entity => TenantBypassed || entity.TenantId == CurrentTenantId);
    }
}
