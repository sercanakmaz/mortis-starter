# Domain layer

Everything here is `src/Domain/Aggregates/<Plural>/` and the kernel in `src/Shared/Domain/`. The example adds three aggregates: `Orders` (root with a child entity and a value object), `Customers` and `Products`.

## Anatomy of an aggregate folder

| File | What it is |
|---|---|
| `Order.cs` | The aggregate root. Extends `AggregateRoot<Guid>`. Properties have `protected set`; children sit in a private list exposed as `IReadOnlyCollection<T>`. All rules live in named methods (`AddLine`, `CancelLine`, `Complete`, `CancelWhenAllLinesCancelled`). It has a public constructor that takes the id, the data and an optional `AuditUser`, and a `protected` parameterless one for EF. |
| `OrderLine.cs` | A child entity. Its constructor and mutating methods are `internal`, so it can only be reached through `Order`. It has no repository, service or command. |
| `Money.cs` | A value object: an immutable `record` that validates itself in its constructor. |
| `OrderLineModel.cs` | A plain input model that a command passes to the service, so a command never builds a child. |
| `OrderStatus.cs` | An enum for the lifecycle (`Open`, `Completed`, `Cancelled`). |
| `Events/*.cs` | One past-tense record per event (`Created`, `LineAdded`, `Completed`). Bare names, ids as strings. |
| `IOrderRepository.cs` | The repository interface, beside its aggregate. It extends `IRepository<Order, Guid>` and adds the reads this aggregate needs (`IsOrderNumberExistsAsync`). |
| `OrderService.cs` | The interface and the class in one file. It orchestrates: check, load, call the aggregate, save. |
| `OrderConstants.cs` | Lengths, codes and every other literal. |
| `OrderModule.cs` | `AddOrdersModule()` registers the service. `DomainModule.cs` calls it. |

`Customer` and `Product` follow the same shape. `Product` shows two things `Order` does not: an alternate key on `Sku` with `FindBySkuTrackingAsync`, and `IncreaseStock` / `DecreaseStock` raising `IQueueEvent` events that async handlers react to.

## Rules that shape the code

- **The service holds no business rule.** `OrderService.CompleteAsync` loads the order with `FindByIdTrackingAsync`, calls `order.Complete()`, calls `order.SetUpdatedAudit(user)`, then `UpdateAsync`. A rule goes to the aggregate or value object.
- **The user is an argument.** Every write method of a service takes `AuditUser user` first: `CreateAsync(AuditUser user, Guid id, ...)`. It reaches the aggregate through the constructor (`new Order(id, ..., user)`) or `SetUpdatedAudit(user)`. There is no injected user context; see [application-layer.md](application-layer.md) for where the user comes from.
- **Events are raised inside the aggregate method that changes the state**, before the repository write. `RaiseEvent` is `protected`, and it stamps the event with the aggregate's current audit user; `SetUpdatedAudit` stamps events raised earlier in the same call. An event therefore always says who caused it (`EventBase.UserId`, `UserEmail`).
- **Ids are `Guid.CreateVersion7()` assigned in the domain; time is `DateTime.UtcNow`.** `BannedSymbols.txt` turns `Guid.NewGuid()`, `DateTime.Now`, `.Result` and `.Wait()` into build errors.
- **A service never injects another aggregate's service.** Another aggregate's data comes from its repository; a reaction in another aggregate is an event handler in the Application layer ([event-handlers.md](event-handlers.md)).
- **A reference to another aggregate is its id** (`Order.CustomerId`) plus a foreign key in the EF configuration.
- **Errors are exceptions from `Shared/Exceptions`:** `NotFoundException` (404), `DuplicateException` (409), `BusinessRuleException` (409, with a code such as `order_not_open`), `InvalidParameterException`, `ReferenceViolationException` (400), `ValidationFailedException` (422), `ForbiddenException`, `TransactionRequiredException`. Each carries a `Code` and an `HttpStatusCode`, which `ExceptionHandler` writes out.

## Adding an aggregate

1. Write the fakes, builders and tests first ([testing.md](testing.md)) and watch them fail.
2. Domain: the aggregate, its events, `I<Aggregate>Repository`, `<Aggregate>Service`, `<Aggregate>Constants`, `<Aggregate>Module`, one line in `DomainModule.cs`.
3. Infrastructure: the repository and the EF configuration ([persistence.md](persistence.md)), one line in `PersistenceModule.cs`, then a migration.
4. Application: the commands, the DTO, the registrations ([application-layer.md](application-layer.md)).
5. Api: a controller. Console: one `AddCommandScenario<...>()` line per command you want to run by hand ([hosts.md](hosts.md)).
