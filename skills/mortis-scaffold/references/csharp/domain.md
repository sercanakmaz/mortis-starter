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
- Every operation is a public method named for the business action. It checks its rules, changes state and raises an event. It never saves.
- A rule that fails throws `BusinessRuleException` with a code from `{Aggregate}Constants`; a bad argument throws `InvalidParameterException`.
- A computed value is an expression-bodied property.

```csharp
private readonly List<OrderLine> _lines = [];
```

```csharp
public string OrderNumber { get; protected set; }
```

```csharp
public IReadOnlyCollection<OrderLine> Lines => _lines.AsReadOnly();
```

```csharp
public decimal TotalAmount => _lines.Where(line => !line.IsCancelled).Sum(line => line.Price.Amount * line.Quantity);
```

```csharp
[DebuggerStepThrough]
protected Order() { }
```

```csharp
public Order(Guid id, string orderNumber, Guid customerId, AuditUser user = null) : base(user)
{
    Id = id;
    OrderNumber = orderNumber;
    CustomerId = customerId;
    Status = OrderStatus.Open;

    RaiseEvent(new Created(Id.ToString(), OrderNumber, CustomerId.ToString()));
}
```

An operation with several rules and a child:

```csharp
public OrderLine AddLine(string sku, Money price, int quantity)
{
    EnsureOpen();

    if (quantity <= 0)
    {
        throw new InvalidParameterException(nameof(quantity), "it must be greater than zero");
    }

    if (_lines.Any(line => !line.IsCancelled && line.Sku == sku))
    {
        throw new DuplicateException(nameof(OrderLine));
    }

    if (_lines.Count > 0 && _lines[0].Price.Currency != price.Currency)
    {
        throw new BusinessRuleException(OrderConstants.MixedCurrencyCode, "Every line of an order uses the same currency.");
    }

    var orderLine = new OrderLine(Guid.CreateVersion7(), Id, sku, price, quantity);

    _lines.Add(orderLine);

    RaiseEvent(new LineAdded(Id.ToString(), orderLine.Id.ToString(), sku, quantity));

    return orderLine;
}
```

An operation that reports whether it changed anything (used by a reaction that must be safe to run twice):

```csharp
public bool CancelWhenAllLinesCancelled()
{
    if (Status != OrderStatus.Open || _lines.Any(line => !line.IsCancelled))
    {
        return false;
    }

    Status = OrderStatus.Cancelled;

    RaiseEvent(new Cancelled(Id.ToString(), OrderNumber, CustomerId.ToString()));

    return true;
}
```

A repeated guard is a private method of the aggregate (aggregates may have private methods; services and handlers may not):

```csharp
private void EnsureOpen()
{
    if (Status != OrderStatus.Open)
    {
        throw new BusinessRuleException(OrderConstants.NotOpenCode, "The order is not open.");
    }
}
```

## Child entity

```csharp
public class OrderLine : Entity<Guid>
```

- Constructor and operations are `internal`: only the root creates and changes it.
- It holds the root's id (`OrderId`) with `{ get; private init; }`.

```csharp
public Guid OrderId { get; private init; }
```

```csharp
internal OrderLine(Guid id, Guid orderId, string sku, Money price, int quantity)
{
    Id = id;
    OrderId = orderId;
    Sku = sku;
    Price = price;
    Quantity = quantity;
}
```

```csharp
internal void Cancel(string reason)
{
    if (IsCancelled)
    {
        throw new BusinessRuleException(OrderConstants.LineAlreadyCancelledCode, "The order line is already cancelled.");
    }

    IsCancelled = true;
    CancellationReason = reason;
}
```

## Value object

A `record` with get-only properties that validates in its constructor and normalises its parts.

```csharp
public record Money
```

```csharp
public Money(decimal amount, string currency)
{
    if (amount < 0)
    {
        throw new InvalidParameterException(nameof(amount), "it cannot be negative");
    }

    if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != OrderConstants.CurrencyLength)
    {
        throw new InvalidParameterException(nameof(currency), "it must be a three-letter code");
    }

    Amount = amount;
    Currency = currency.Trim().ToUpperInvariant();
}
```

## Status, constants, input model

```csharp
namespace {Name}.Domain.Aggregates.Orders;

public enum OrderStatus
{
    Open,
    Completed,
    Cancelled
}
```

```csharp
public static class OrderConstants
```

```csharp
public const string NotOpenCode = "order_not_open";
```

```csharp
public const int OrderNumberMaxLength = 64;
```

- A rule code is `<aggregate>_<rule>` in snake case. A length constant is used by the validator, the mapping and nowhere else.

```csharp
public record OrderLineModel(string Sku, decimal Price, string Currency, int Quantity);
```

## Domain events

One file per event in `Events/`, namespace `{Name}.Domain.Aggregates.{Plural}.Events`. A positional record, past tense, ids as strings. An event that leaves the transaction also implements `IQueueEvent`.

```csharp
public record Created(string Id, string OrderNumber, string CustomerId) : EventBase;
```

```csharp
public record LineAdded(string Id, string LineId, string Sku, int Quantity) : EventBase, IQueueEvent;
```

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

public class CreditPolicy(IOrderRepository orderRepository) : ICreditPolicy
{
    public async Task EnsureFitsAsync(Customer customer, decimal orderTotal)
    {
        var openTotal = await orderRepository.SumOpenTotalOfCustomerAsync(customer.Id);

        if (openTotal + orderTotal > customer.CreditLimit)
        {
            throw new BusinessRuleException(CustomerConstants.CreditExceededCode, "The order exceeds the customer's credit limit.");
        }
    }
}
```

- It may read through repositories and call ports. It never saves; the command handler does.
- It has no private methods.

## Anti-corruption port

An interface in `Ports/`, in the domain's words, implemented in Infrastructure (`Integrations/`).

```csharp
public interface ITaxRates
{
    Task<decimal> RateForAsync(string countryCode);
}
```

## Registration

File `DomainModule.cs`, namespace `{Name}.Domain`. One line per domain service; with none, only `return services;`.

```csharp
public static class DomainModule
```

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

| File | Namespace | Usings |
|---|---|---|
| `src/{Name}.Domain/DomainModule.cs` | `{Name}.Domain` | Microsoft.Extensions.DependencyInjection, {Name}.Domain.Aggregates.Customers, {Name}.Domain.Aggregates.Orders, {Name}.Domain.Aggregates.Products |
