using Microsoft.EntityFrameworkCore.Storage;
using Boilerplate.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Boilerplate.Shared.Domain;

namespace Boilerplate.Infrastructure.Persistence;

public class UnitOfWork(
    AppDbContext context,
    EventContext eventContext,
    EventHandlerExecutor eventHandlerExecutor,
    IEnumerable<IEventDispatcher> eventDispatchers,
    ILogger<UnitOfWork> logger) : IUnitOfWork
{
    private readonly List<Func<Task>> _committedActions = [];
    private IDbContextTransaction _transaction;
    private string _transactionKey;

    public bool HasTransaction => _transaction is not null;

    public async Task<int> SaveChangesAsync()
    {
        try
        {
            return await context.SaveChangesAsync();
        }
        catch (DbUpdateException exception)
        {
            DatabaseExceptionHandler.Handle(exception);

            throw;
        }
    }

    public async Task BeginTransactionAsync(string transactionKey)
    {
        if (_transaction is not null) return;

        _transaction = await context.Database.BeginTransactionAsync();
        _transactionKey = transactionKey;
    }

    public async Task CommitTransactionAsync(string transactionKey)
    {
        if (_transaction is null || transactionKey != _transactionKey) return;

        await SaveChangesAsync();

        await eventHandlerExecutor.ExecuteRaisedAsync();

        await _transaction.CommitAsync();
        await _transaction.DisposeAsync();

        _transaction = null;
        _transactionKey = null;

        while (eventContext.TakeDispatched() is { } @event)
        {
            if (@event is not IQueueEvent) continue;

            foreach (var eventDispatcher in eventDispatchers)
            {
                try
                {
                    await eventDispatcher.Publish(@event);
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Publishing {EventType} {EventId} failed after the transaction was committed", @event.GetType().FullName, @event.EventId);
                }
            }
        }

        var committedActions = _committedActions.ToList();

        _committedActions.Clear();

        foreach (var committedAction in committedActions)
        {
            await committedAction();
        }
    }

    public async Task RollbackTransactionAsync()
    {
        eventContext.Clear();

        _committedActions.Clear();

        if (_transaction is null) return;

        await _transaction.RollbackAsync();
        await _transaction.DisposeAsync();

        _transaction = null;
        _transactionKey = null;

        context.ChangeTracker.Clear();
    }

    public void OnCommitted(Func<Task> action)
    {
        _committedActions.Add(action);
    }
}
