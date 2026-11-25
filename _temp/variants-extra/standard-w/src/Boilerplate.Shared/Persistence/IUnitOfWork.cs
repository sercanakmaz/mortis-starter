namespace Boilerplate.Shared.Persistence;

public interface IUnitOfWork
{
    bool HasTransaction { get; }
    Task<int> SaveChangesAsync();
    Task BeginTransactionAsync(string transactionKey);
    Task CommitTransactionAsync(string transactionKey);
    Task RollbackTransactionAsync();
    void OnCommitted(Func<Task> action);
}
