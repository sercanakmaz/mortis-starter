# Testing

xUnit and Moq, `Assert` only. The tests mirror the source folders; everything that is not a `[Fact]` or `[Theory]` lives in `test/Tests/TestSupport/`.

## Rules

- A test class holds test methods and nothing else: no fields, helpers or nested types. Mocks, fakes and builders are in `TestSupport/`.
- A test file is named after the type it tests (`OrderServiceTests.cs`). A test is `Method_Should_Behaviour` or `Method_WhenCondition_Should_Behaviour`.
- Write the failing test first and take its failure from a real run. Never narrow a scenario, fit an expected value to the output, or weaken an assertion that went red.
- Locals are named after the thing: `orderRepository`, `fakeOrder`.

## TestSupport conventions

| Kind | Example | Use |
|---|---|---|
| Fake aggregate | `FakeOrder.Create().WithLine("SKU-1").WithCancelledLine("SKU-1")` | A real aggregate in a chosen state, built through its own methods. `WithoutEvents()` drops raised events. |
| Repository builder | `OrderRepositoryBuilder.Create().WithFindByIdTrackingAsync(order).WithUpdateAsync(callback)` | Wraps a Moq mock; `Build()` gives the interface, `BuildMock()` the mock for `Verify`. |
| Service builder | `OrderServiceBuilder.Create().WithOrderRepository(...).Build()` | A real service over a mocked repository. |
| Dispatcher builder | `CommandDispatcherBuilder.Create().WithCommandHandler(handler).Build()` | A real `CommandDispatcher` that knows only the handlers you give it; how an event handler or a scenario is tested. |
| Pipeline builder | `CommandPipelineBuilder<TCommand>.Create().WithValidator(...).Build()` | The real pipeline with a fake unit of work. |
| Fake user | `FakeAuditUser.Create()` | The `AuditUser` that tests pass to services and aggregates. |
| Kernel fakes | `FakeAggregate`, `FakeEvent`, `FakeCommand`, `FakeUserCommand`, `Recording*EventHandler`, `RecordingLogger<T>` | Kernel tests that do not depend on an example aggregate. |
| Database | `AppDbContextBuilder` | A context pointed at a server that is never contacted: enough for the model and the change tracker, so repository and configuration tests need no database. |

## What gets a test

| Code | Test |
|---|---|
| Aggregate and value object | `Domain/<Plural>/<Aggregate>Tests.cs`: every rule, every raised event. |
| Service | `<Aggregate>ServiceTests.cs`: the order of statements (events are raised before the repository write), duplicates, not-found. |
| Command handler | `Application/<Plural>/<Command>HandlerTests.cs`: the service is called with the command's data and the result is mapped. |
| Event handler | `Application/<Plural>/<Handler>Tests.cs`: the command it builds, the user it carries (the event's user for a sync handler, `SystemUser` for an async one), and the effect. |
| Repository, configuration | `Infrastructure/Persistence/<Plural>/`: change-tracker state and model shape (keys, foreign keys, check constraints through `IDesignTimeModel`). |
| Kernel | `Shared/Application/CommandDispatcherTests.cs` and the rest of `Shared/...`, `Api/Filters/UserCommandFilterTests.cs`, `Console/CommandScenarioTests.cs`, `Infrastructure/Messaging/AsyncEventHandlerDispatcherTests.cs`. |

Behaviour that only a real PostgreSQL shows (foreign keys firing, the `xmin` token, concurrent writers) is checked by running Console scenarios and the Api against a database; the skills repository's `tests/smoke.sh` does that.
