using Boilerplate.Shared.Persistence;
using Boilerplate.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using Boilerplate.Shared.Domain;

namespace Boilerplate.Infrastructure.Persistence;

public abstract class EfRepository<TAggregate, TKey>(AppDbContext context, IUnitOfWork unitOfWork, EventContext eventContext) : IRepository<TAggregate, TKey> where TAggregate : AggregateRoot<TKey>
{
    protected readonly DbSet<TAggregate> DbSet = context.Set<TAggregate>();

    public virtual Task CreateAsync(TAggregate aggregate)
    {
        EnsureTransaction();

        DbSet.Add(aggregate);

        RaiseEvents(aggregate);

        return unitOfWork.SaveChangesAsync();
    }

    public virtual Task UpdateAsync(TAggregate aggregate)
    {
        EnsureTransaction();

        if (context.Entry(aggregate).State == EntityState.Detached)
        {
            throw new InvalidOperationException($"{typeof(TAggregate).Name} is not tracked. Load it with FindByIdTrackingAsync before UpdateAsync.");
        }

        RaiseEvents(aggregate);

        return unitOfWork.SaveChangesAsync();
    }

    public virtual Task DeleteAsync(TAggregate aggregate)
    {
        EnsureTransaction();

        DbSet.Remove(aggregate);

        RaiseEvents(aggregate);

        return unitOfWork.SaveChangesAsync();
    }

    public virtual Task<TAggregate> FindByIdAsync(TKey id)
    {
        return DbSet.AsNoTracking().FirstOrDefaultAsync(aggregate => aggregate.Id.Equals(id));
    }

    public virtual Task<TAggregate> FindByIdTrackingAsync(TKey id)
    {
        return DbSet.FirstOrDefaultAsync(aggregate => aggregate.Id.Equals(id));
    }

    protected IQueryable<TAggregate> Query()
    {
        return DbSet.AsNoTracking();
    }

    protected void RaiseEvents(TAggregate aggregate)
    {
        foreach (var @event in aggregate.DomainEvents ?? [])
        {
            eventContext.AddRaised(@event);
        }

        aggregate.ClearEvents();
    }

    protected void EnsureTransaction()
    {
        if (!unitOfWork.HasTransaction)
        {
            throw new TransactionRequiredException(typeof(TAggregate).Name);
        }
    }
}
