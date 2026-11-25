using Boilerplate.Infrastructure.Messaging.Outbox;
using Boilerplate.Shared.Domain;
using Boilerplate.Shared.Persistence;
using MongoDB.Driver;

namespace Boilerplate.Infrastructure.Persistence.Mongo;

public class MongoUnitOfWork(
    MongoSession mongoSession,
    EventContext eventContext,
    EventHandlerExecutor eventHandlerExecutor,
    OutboxRepository outboxRepository,
    OutboxWorker outboxWorker) : IUnitOfWork
{
    private readonly Dictionary<object, long> _versions = new(ReferenceEqualityComparer.Instance);
    private readonly List<Func<Task>> _committedActions = [];
    private string _transactionKey;

    public IClientSessionHandle Session => mongoSession.Handle;
    public bool HasTransaction => Session?.IsInTransaction == true;

    public Task<int> SaveChangesAsync()
    {
        return Task.FromResult(0);
    }

    public async Task BeginTransactionAsync(string transactionKey)
    {
        if (HasTransaction) return;

        var session = await mongoSession.StartAsync();

        session.StartTransaction(new TransactionOptions(ReadConcern.Snapshot, writeConcern: WriteConcern.WMajority));
        _transactionKey = transactionKey;
    }

    public async Task CommitTransactionAsync(string transactionKey)
    {
        if (!HasTransaction || transactionKey != _transactionKey) return;

        await eventHandlerExecutor.ExecuteRaisedAsync();

        var queueEvents = new List<EventBase>();

        while (eventContext.TakeDispatched() is { } @event)
        {
            if (@event is IQueueEvent) queueEvents.Add(@event);
        }

        await outboxRepository.AddAsync(queueEvents);
        await Session.CommitTransactionAsync();

        _transactionKey = null;
        _versions.Clear();

        foreach (var queueEvent in queueEvents)
        {
            await outboxWorker.PublishAsync(queueEvent.EventId);
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
        _versions.Clear();

        if (!HasTransaction) return;

        await Session.AbortTransactionAsync();

        _transactionKey = null;
    }

    public void OnCommitted(Func<Task> action)
    {
        _committedActions.Add(action);
    }

    public void Remember(object aggregate, long version)
    {
        _versions[aggregate] = version;
    }

    public long VersionOf(object aggregate)
    {
        return _versions.TryGetValue(aggregate, out var version)
            ? version
            : throw new InvalidOperationException($"{aggregate.GetType().Name} is not tracked. Load it with FindByIdTrackingAsync before UpdateAsync.");
    }
}
