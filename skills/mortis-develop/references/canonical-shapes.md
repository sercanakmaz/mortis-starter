# Canonical shapes

The closed vocabulary of type names. A new type takes one of these shapes; a name outside the list needs a reason written in `docs/adr/`. The example domain is the slice to copy from: Products and Orders show every shape below.

| Shape | Name | Where | Copy from |
|---|---|---|---|
| aggregate root | `{Aggregate}` (singular noun) | `Domain/Aggregates/{Plural}/` | `Order` |
| child entity | `{Aggregate}{Part}` | same folder | `OrderLine` |
| value object | a noun (`Money`) | same folder, or `Shared/Domain` when two aggregates use it | `Money` |
| event | past tense (`Created`, `LineAdded`) | `Domain/Aggregates/{Plural}/Events/` | `LineAdded` |
| constants | `{Aggregate}Constants` | `Domain/Aggregates/{Plural}/` | `OrderConstants` |
| repository port | `I{Aggregate}Repository` | `Domain/Aggregates/{Plural}/` | `IOrderRepository` |
| read port | `I{Aggregate}{Purpose}Reader` | `Domain/Ports/` | none yet; `queries.md` for the shape |
| domain service | `I{Rule}Service` and `{Rule}Service` in one file | `Domain/Services/` | none in the example; `domain.md` |
| command | `{Verb}{Aggregate}Command` | `Application/{Plural}/` | `CreateOrderCommand` |
| reaction command | `{Verb}{Aggregate}When{Condition}Command` | `Application/{Plural}/` | `CancelOrderWhenAllLinesCancelledCommand` |
| query (split, read-models) | `Get{Aggregate}Query`, `List{Plural}Query` | `Application/{Plural}/` | `queries.md` |
| sync event handler | `{Action}When{Aggregate}{Event}EventHandler` | the reacting aggregate's `Application/{Plural}/` | `CancelOrderWhenOrderLineCancelledEventHandler` |
| event consumer | `{Action}When{Aggregate}{Event}EventConsumer` | the reacting aggregate's `Application/{Plural}/` | `DecreaseStockWhenOrderLineAddedEventConsumer` |
| data transfer object | `{Aggregate}Dto` with `Map(...)` | `Application/{Plural}/` | `OrderDto` |
| configuration | `{Aggregate}Configuration` | `Infrastructure/Persistence/{Plural}/` | `OrderConfiguration` |
| repository | `{Aggregate}Repository` | `Infrastructure/Persistence/{Plural}/` | `OrderRepository` |
| controller | `{Plural}Controller` | `Api/Controllers/` | `OrdersController` |
| fake | `Fake{Aggregate}` | `test/.../TestSupport/Domain/{Plural}/` | `FakeOrder` |
| builder | `{Port}Builder` | `test/.../TestSupport/` mirrored | `OrderRepositoryBuilder` |

Never: `{Aggregate}Service`, `Manager`, `Helper`, `Util`, `Processor`, a `Base` prefix outside the kernel, a mediator, a mapping library.
