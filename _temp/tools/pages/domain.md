# Domain: aggregates, events, repositories, domain services, C#

Project `{Name}.Domain`. References only Shared. Layer 1: `layers.md` "Domain", `building-blocks.md`. Names and paths: `naming.md`. The snippets are from the order aggregate of the example; `worked-slice.md` has the whole slice.

## Folder of an aggregate

```
src/{Name}.Domain/
  DomainModule.cs
  Aggregates/{Plural}/
    {Aggregate}.cs                the root
    <Child>.cs, <Value>.cs         child entities and value objects
    {Aggregate}Status.cs           when it has a status
    {Aggregate}Constants.cs        rule codes and maximum lengths
    <Noun>Model.cs                 an input shape commands share
    I{Aggregate}Repository.cs
    Events/<Event>.cs
  Services/<Concept>.cs            domain services, only when needed
  Ports/I<Question>.cs             anti-corruption ports, only when needed
```

There is no `{Aggregate}Service`. Command handlers load, call the aggregate and save (`application.md`).

## Aggregate root

```csharp
public class Order : AggregateRoot<Guid>
```

- State is `{ get; protected set; }`; a collection is a private `List<T>` exposed as `IReadOnlyCollection<T>`.
- A protected parameterless constructor for the mapper, marked `[DebuggerStepThrough]` (`using System.Diagnostics;`).
- A public constructor takes the id, the data and `AuditUser user = null`, passes `user` to the base, and raises `Created`.
- **An aggregate whose key the database assigns** (`int` or `long`, `keys.default` or chosen for that aggregate) takes no id, raises nothing in its constructor, and has an operation `MarkCreated()` that raises `Created` with the real id; the handler calls it after `CreateAsync` (`worked-slice.md`, Categories). Its create command has no `Id`; references to it are `int?` or `int`.
- Every operation is a public method named for the business action. It checks its rules, changes state and raises an event. It never saves.
- A rule that fails throws `BusinessRuleException` with a code from `{Aggregate}Constants`; a bad argument throws `InvalidParameterException`.
- A computed value is an expression-bodied property.

@@member src/Boilerplate.Domain/Aggregates/Orders/Order.cs::Order::_lines@@

@@member src/Boilerplate.Domain/Aggregates/Orders/Order.cs::Order::OrderNumber@@

@@member src/Boilerplate.Domain/Aggregates/Orders/Order.cs::Order::Lines@@

@@member src/Boilerplate.Domain/Aggregates/Orders/Order.cs::Order::TotalAmount@@

@@member src/Boilerplate.Domain/Aggregates/Orders/Order.cs::Order::Order#1@@

@@member src/Boilerplate.Domain/Aggregates/Orders/Order.cs::Order::Order#2@@

An operation with several rules and a child:

@@member src/Boilerplate.Domain/Aggregates/Orders/Order.cs::Order::AddLine@@

An operation that reports whether it changed anything (used by a reaction that must be safe to run twice):

@@member src/Boilerplate.Domain/Aggregates/Orders/Order.cs::Order::CancelWhenAllLinesCancelled@@

A repeated guard is a private method of the aggregate (aggregates may have private methods; services and handlers may not):

@@member src/Boilerplate.Domain/Aggregates/Orders/Order.cs::Order::EnsureOpen@@

## Child entity

```csharp
public class OrderLine : Entity<Guid>
```

- Constructor and operations are `internal`: only the root creates and changes it.
- It holds the root's id (`OrderId`) with `{ get; private init; }`.

@@member src/Boilerplate.Domain/Aggregates/Orders/OrderLine.cs::OrderLine::OrderId@@

@@member src/Boilerplate.Domain/Aggregates/Orders/OrderLine.cs::OrderLine::OrderLine#2@@

@@member src/Boilerplate.Domain/Aggregates/Orders/OrderLine.cs::OrderLine::Cancel@@

## Value object

A `record` with get-only properties that validates in its constructor and normalises its parts.

```csharp
public record Money
```

@@member src/Boilerplate.Domain/Aggregates/Orders/Money.cs::Money::Money@@

## Status, constants, input model

@@data src/Boilerplate.Domain/Aggregates/Orders/OrderStatus.cs csharp@@

@@decl src/Boilerplate.Domain/Aggregates/Orders/OrderConstants.cs::OrderConstants@@

@@member src/Boilerplate.Domain/Aggregates/Orders/OrderConstants.cs::OrderConstants::NotOpenCode@@

@@member src/Boilerplate.Domain/Aggregates/Orders/OrderConstants.cs::OrderConstants::OrderNumberMaxLength@@

- A rule code is `<aggregate>_<rule>` in snake case. A length constant is used by the validator, the mapping and nowhere else.

@@decl src/Boilerplate.Domain/Aggregates/Orders/OrderLineModel.cs::OrderLineModel@@

## Domain events

One file per event in `Events/`, namespace `{Name}.Domain.Aggregates.{Plural}.Events`. A positional record, past tense, ids as strings. An event that leaves the transaction also implements `IQueueEvent`.

@@decl src/Boilerplate.Domain/Aggregates/Orders/Events/Created.cs::Created@@

@@decl src/Boilerplate.Domain/Aggregates/Orders/Events/LineAdded.cs::LineAdded@@

- Raised inside an operation, never after the repository write.
- Named bare (`Completed`) when `naming.events` is `bare`; `OrderCompleted` when `prefixed`.

## Repository interface

```csharp
public interface IOrderRepository : IRepository<Order, Guid>;
```

A query the domain needs is added by name:

```csharp
public interface IProductRepository : IRepository<Product, Guid>
{
    Task<Product> FindBySkuTrackingAsync(string sku);
}
```

- Uniqueness is never a repository query: the database's unique index refuses the duplicate.

## Domain service

Only when a decision fits no single aggregate: it needs several entities, or an answer from another system. Stateless, in `Services/`, named for its concept, with an interface beside it in the same file, registered in `DomainModule`. The example domain has none; the snippet below is an illustration, not a file to generate.

```csharp
public interface ICreditPolicy
{
    Task EnsureFitsAsync(Customer customer, decimal orderTotal);
}

public class CreditPolicy(IOpenOrderTotals openOrderTotals) : ICreditPolicy
{
    public async Task EnsureFitsAsync(Customer customer, decimal orderTotal)
    {
        var openTotal = await openOrderTotals.OfCustomerAsync(customer.Id);

        if (openTotal + orderTotal > customer.CreditLimit)
        {
            throw new BusinessRuleException(CustomerConstants.CreditExceededCode, "The order exceeds the customer's credit limit.");
        }
    }
}
```

- **Pure by default:** the handler passes in the aggregates it loaded.
- **It may read** through an interface declared in Domain when the rule decides what data it needs: a narrow read port in `Ports/` (`IOpenOrderTotals` above, implemented in `Infrastructure/Persistence/{Plural}/`) in preference to a repository, and only read methods.
- **It never writes:** no `CreateAsync`, `UpdateAsync`, `DeleteAsync`, no `IUnitOfWork`, no transaction. It changes the aggregates it was given or returns a new one; the command handler saves. `verify.sh` fails a domain file that calls a write method or takes `IUnitOfWork`.
- It has no private methods.

## Anti-corruption port

An interface in `Ports/`, in the domain's words. A port to another system is implemented in `Infrastructure/Integrations/`; a read port over our own storage in `Infrastructure/Persistence/{Plural}/`.

```csharp
public interface ITaxRates
{
    Task<decimal> RateForAsync(string countryCode);
}
```

## Registration

File `DomainModule.cs`, namespace `{Name}.Domain`. One line per domain service; with none, only `return services;`.

@@decl src/Boilerplate.Domain/DomainModule.cs::DomainModule@@

```csharp
public static IServiceCollection AddDomainModule(this IServiceCollection services)
{
    return services;
}
```

A domain service adds one line before the `return`, for example `services.AddScoped<ICreditPolicy, CreditPolicy>();`. The example domain adds none.

## Tests

`{Aggregate}Tests` for every operation's success and every rule's failure; `{Value}Tests` for every value object. Snippets in `worked-slice.md`.

## Namespaces and usings

Every file of this page, with its namespace and the `using` lines it needs (`ImplicitUsings` covers `System`, `System.Linq`, `System.Threading.Tasks` and the like). A file the table does not list follows the namespace rule in `naming.md`.

@@usings domain.md@@
