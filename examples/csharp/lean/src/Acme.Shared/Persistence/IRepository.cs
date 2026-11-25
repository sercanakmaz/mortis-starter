using Acme.Shared.Domain;

namespace Acme.Shared.Persistence;

public interface IRepository<TAggregate, in TKey> where TAggregate : AggregateRoot<TKey>
{
    Task CreateAsync(TAggregate aggregate);

    Task UpdateAsync(TAggregate aggregate);

    Task DeleteAsync(TAggregate aggregate);

    Task<TAggregate> FindByIdAsync(TKey id);

    Task<TAggregate> FindByIdTrackingAsync(TKey id);
}
