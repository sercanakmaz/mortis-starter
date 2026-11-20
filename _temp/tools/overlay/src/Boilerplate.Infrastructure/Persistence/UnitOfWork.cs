using Microsoft.EntityFrameworkCore.Storage;
using Boilerplate.Infrastructure.Messaging;
using Boilerplate.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Boilerplate.Shared.Domain;

namespace Boilerplate.Infrastructure.Persistence;

public class UnitOfWork(
    AppDbContext context,
    EventContext eventContext,
    EventHandlerExecutor eventHandlerExecutor,
    EventPublisher eventPublisher,
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

        var queueEvents = new List<EventBase>();

        while (eventContext.TakeDispatched() is { } @event)
        {
            if (@event is IQueueEvent) queueEvents.Add(@event);
        }

        await _transaction.CommitAsync();
        await _transaction.DisposeAsync();

        _transaction = null;
        _transactionKey = null;

        foreach (var queueEvent in queueEvents)
        {
            try
            {
                await eventPublisher.PublishAsync(queueEvent);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Publishing {Event} {EventId} failed after the transaction was committed", queueEvent.GetType().Name, queueEvent.EventId);
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
