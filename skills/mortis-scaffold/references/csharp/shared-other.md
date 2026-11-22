# Shared: failures, persistence contracts and registration, C#

Project `{Name}.Shared`, folders `Exceptions/` and `Persistence/`, and `SharedModule.cs` at the project root.

## The failure family

Folder `Exceptions/`, namespace `{Name}.Shared.Exceptions`, one file per type, each needs `using System.Net;`. Every failure a user can see is an `ExceptionBase`: a stable code, an HTTP status, a message. The host turns it into `{ code, message, errors }`. With `languages.mode` other than `none` it also carries its parameters and the host translates the message (`languages.md`).

```csharp
public abstract class ExceptionBase(string code, HttpStatusCode httpStatusCode, string message, Exception innerException = null) : Exception(message, innerException)
```

```csharp
public string Code { get; } = code;
```

```csharp
public HttpStatusCode HttpStatusCode { get; } = httpStatusCode;
```

The subclasses are one declaration each, written exactly so:

```csharp
public class BusinessRuleException(string code, string message) : ExceptionBase(code, HttpStatusCode.Conflict, message);
```

```csharp
public class NotFoundException(string name) : ExceptionBase("not_found", HttpStatusCode.NotFound, $"{name} was not found.");
```

```csharp
public class DuplicateException(string name, Exception innerException = null) : ExceptionBase("duplicate", HttpStatusCode.Conflict, $"{name} already exists.", innerException);
```

```csharp
public class InvalidParameterException(string name, string reason, Exception innerException = null) : ExceptionBase("invalid_parameter", HttpStatusCode.BadRequest, $"{name} is invalid: {reason}", innerException);
```

```csharp
public class ReferenceViolationException(string constraintName, Exception innerException = null) : ExceptionBase("reference_violation", HttpStatusCode.BadRequest, $"The reference {constraintName} points at a missing record, or the record is still referenced.", innerException);
```

```csharp
public class ForbiddenException(string action) : ExceptionBase("forbidden", HttpStatusCode.Forbidden, $"{action} is not allowed.");
```

```csharp
public class TransactionRequiredException(string aggregateName) : ExceptionBase("transaction_required", HttpStatusCode.InternalServerError, $"A write to {aggregateName} ran outside a transaction. The command must implement ITransactionalCommand.");
```

```csharp
public class ValidationFailedException(IDictionary<string, string[]> errors) : ExceptionBase("validation_failed", HttpStatusCode.UnprocessableEntity, "The request is not valid.")
```

```csharp
public IDictionary<string, string[]> Errors { get; } = errors;
```

| Type | Code | Status | Raised by |
|---|---|---|---|
| `BusinessRuleException` | the rule's own code from `{Aggregate}Constants` | 409 | an aggregate operation |
| `NotFoundException` | `not_found` | 404 | a handler that loads by id |
| `DuplicateException` | `duplicate` | 409 | a unique index, through `DatabaseExceptionHandler` |
| `InvalidParameterException` | `invalid_parameter` | 400 | a value object or aggregate constructor; a check constraint |
| `ReferenceViolationException` | `reference_violation` | 400 | a foreign key, through `DatabaseExceptionHandler` |
| `ForbiddenException` | `forbidden` | 403 | an authorisation rule |
| `TransactionRequiredException` | `transaction_required` | 500 | a repository write outside a transaction |
| `ValidationFailedException` | `validation_failed` | 422 | the pipeline, from validators |

## Persistence contracts

Folder `Persistence/`, namespace `{Name}.Shared.Persistence`. Implemented in Infrastructure (`infrastructure-postgres.md`).

```csharp
public interface IRepository<TAggregate, in TKey> where TAggregate : AggregateRoot<TKey>
```

```csharp
Task CreateAsync(TAggregate aggregate);
```

```csharp
Task UpdateAsync(TAggregate aggregate);
```

```csharp
Task DeleteAsync(TAggregate aggregate);
```

```csharp
Task<TAggregate> FindByIdAsync(TKey id);
```

```csharp
Task<TAggregate> FindByIdTrackingAsync(TKey id);
```

```csharp
public interface IUnitOfWork
```

```csharp
bool HasTransaction { get; }
```

```csharp
Task<int> SaveChangesAsync();
```

```csharp
Task BeginTransactionAsync(string transactionKey);
```

```csharp
Task CommitTransactionAsync(string transactionKey);
```

```csharp
Task RollbackTransactionAsync();
```

```csharp
void OnCommitted(Func<Task> action);
```

- `FindByIdAsync` reads without tracking; `FindByIdTrackingAsync` is the read you change and save.

## SharedModule

File `SharedModule.cs`, namespace `{Name}.Shared`:

```csharp
public static class SharedModule
```

```csharp
public static IServiceCollection AddSharedModule(this IServiceCollection services)
{
    services.AddScoped<EventContext>();
    services.AddScoped<EventHandlerExecutor>();
    services.AddScoped(typeof(CommandPipeline<>));
    services.AddScoped<ICommandDispatcher, CommandDispatcher>();

    return services;
}
```

- Registration is explicit: one line per service. Assembly scanning is never used.

## Namespaces and usings

Every file of this page, with its namespace and the `using` lines it needs (`ImplicitUsings` covers `System`, `System.Linq`, `System.Threading.Tasks` and the like). A file the table does not list follows the namespace rule in `naming.md`.

| File | Namespace | Usings |
|---|---|---|
| `src/{Name}.Shared/Exceptions/BusinessRuleException.cs` | `{Name}.Shared.Exceptions` | System.Net |
| `src/{Name}.Shared/Exceptions/DuplicateException.cs` | `{Name}.Shared.Exceptions` | System.Net |
| `src/{Name}.Shared/Exceptions/ExceptionBase.cs` | `{Name}.Shared.Exceptions` | System.Net |
| `src/{Name}.Shared/Exceptions/ForbiddenException.cs` | `{Name}.Shared.Exceptions` | System.Net |
| `src/{Name}.Shared/Exceptions/InvalidParameterException.cs` | `{Name}.Shared.Exceptions` | System.Net |
| `src/{Name}.Shared/Exceptions/NotFoundException.cs` | `{Name}.Shared.Exceptions` | System.Net |
| `src/{Name}.Shared/Exceptions/ReferenceViolationException.cs` | `{Name}.Shared.Exceptions` | System.Net |
| `src/{Name}.Shared/Exceptions/TransactionRequiredException.cs` | `{Name}.Shared.Exceptions` | System.Net |
| `src/{Name}.Shared/Exceptions/ValidationFailedException.cs` | `{Name}.Shared.Exceptions` | System.Net |
| `src/{Name}.Shared/Persistence/IRepository.cs` | `{Name}.Shared.Persistence` | {Name}.Shared.Domain |
| `src/{Name}.Shared/Persistence/IUnitOfWork.cs` | `{Name}.Shared.Persistence` | none |
| `src/{Name}.Shared/SharedModule.cs` | `{Name}.Shared` | Microsoft.Extensions.DependencyInjection, {Name}.Shared.Application, {Name}.Shared.Domain |
