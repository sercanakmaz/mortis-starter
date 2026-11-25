namespace Boilerplate.Infrastructure.Persistence.Mongo;

public class ConcurrencyConflictException(string name) : Exception($"{name} was changed by someone else since it was loaded.");
