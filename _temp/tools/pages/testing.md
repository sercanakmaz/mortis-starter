# Testing, C#

One test project, `test/{Name}.Tests` (xUnit, Moq). Its folders mirror `src/`: a test of `src/{Name}.Domain/Aggregates/Orders/Order.cs` is `test/{Name}.Tests/Domain/Orders/OrderTests.cs`. Fakes, builders and recorders live only under `TestSupport/`, mirrored the same way. The namespace follows the path: `{Name}.Tests.Domain.Orders`, `{Name}.Tests.TestSupport.Domain.Orders`. Every file of this page is in the table at the end, with its namespace and usings.

## Rules

- A test class holds only methods marked `[Fact]` or `[Theory]`. No fields, no constructor, no helper method: what a test needs comes from `TestSupport`.
- Assertions use xUnit `Assert` only.
- Names: `Method_Condition_Should_Result` or `Method_Should_Result`.
- Arrange, act, assert, separated by one blank line.
- A mock is wrapped in a builder: `{Type}Builder.Create().With...(...).Build()`, and `BuildMock()` when a test verifies a call.
- A fake aggregate is a subclass, `Fake{Aggregate}`, with `Create()` and `With...()` methods that call the real operations.
- No test talks to a database, the network or the clock. Persistence tests build the context against a connection string that is never opened.
- `Guid.NewGuid()`, `DateTime.Now`, `.Result` and `.Wait()` are build errors here too.

## Test support for the kernel

Every type below is written in every project, example or not.

### Shared/Application

File `TestSupport/Shared/Application/FakeCommand.cs` holds four types:

@@decl test/Boilerplate.Tests/TestSupport/Shared/Application/FakeCommand.cs::FakeCommand@@

@@member test/Boilerplate.Tests/TestSupport/Shared/Application/FakeCommand.cs::FakeCommand::Name@@

@@decl test/Boilerplate.Tests/TestSupport/Shared/Application/FakeCommand.cs::FakeTransactionalCommand@@

@@member test/Boilerplate.Tests/TestSupport/Shared/Application/FakeCommand.cs::FakeTransactionalCommandValidator::FakeTransactionalCommandValidator@@

@@members test/Boilerplate.Tests/TestSupport/Shared/Application/FakeCommand.cs::FakeContextCommand@@

`FakeTransactionalCommand` has the same `Name` property; `FakeTransactionalCommandValidator` is `AbstractValidator<FakeTransactionalCommand>`.

Three handlers record what they handled. `FakeCommandHandler.cs`:

@@members test/Boilerplate.Tests/TestSupport/Shared/Application/FakeCommandHandler.cs::FakeCommandHandler@@

`FakeTransactionalCommandHandler.cs` and `FakeContextCommandHandler.cs` are the same for `FakeTransactionalCommand` and `FakeContextCommand`.

@@members test/Boilerplate.Tests/TestSupport/Shared/Application/CommandPipelineBuilder.cs::CommandPipelineBuilder@@

@@members test/Boilerplate.Tests/TestSupport/Shared/Application/CommandDispatcherBuilder.cs::CommandDispatcherBuilder@@

@@members test/Boilerplate.Tests/TestSupport/Shared/Application/CommandHandlerMiddlewareBuilder.cs::CommandHandlerMiddlewareBuilder@@

### Shared/Domain

@@members test/Boilerplate.Tests/TestSupport/Shared/Domain/FakeAuditUser.cs::FakeAuditUser@@

File `FakeEvent.cs`, three records:

@@decl test/Boilerplate.Tests/TestSupport/Shared/Domain/FakeEvent.cs::FakeEvent@@

@@decl test/Boilerplate.Tests/TestSupport/Shared/Domain/FakeEvent.cs::FakeCascadedEvent@@

@@decl test/Boilerplate.Tests/TestSupport/Shared/Domain/FakeEvent.cs::FakeQueueEvent@@

@@members test/Boilerplate.Tests/TestSupport/Shared/Domain/FakeAggregate.cs::FakeAggregate@@

File `RecordingEventHandler.cs` holds two handlers:

@@members test/Boilerplate.Tests/TestSupport/Shared/Domain/RecordingEventHandler.cs::RecordingEventHandler@@

@@members test/Boilerplate.Tests/TestSupport/Shared/Domain/RecordingEventHandler.cs::ThrowingEventHandler@@

@@members test/Boilerplate.Tests/TestSupport/Shared/Domain/RecordingAsyncEventHandler.cs::RecordingAsyncEventHandler@@

@@members test/Boilerplate.Tests/TestSupport/Shared/Domain/EventHandlerExecutorBuilder.cs::EventHandlerExecutorBuilder@@

### Shared/Persistence

@@members test/Boilerplate.Tests/TestSupport/Shared/Persistence/UnitOfWorkBuilder.cs::UnitOfWorkBuilder@@

### Infrastructure

@@members test/Boilerplate.Tests/TestSupport/Infrastructure/Persistence/AppDbContextBuilder.cs::AppDbContextBuilder@@

@@members test/Boilerplate.Tests/TestSupport/Infrastructure/Messaging/RecordingLogger.cs::RecordingLogger@@

@@members test/Boilerplate.Tests/TestSupport/Infrastructure/Messaging/AsyncEventHandlerDispatcherBuilder.cs::AsyncEventHandlerDispatcherBuilder@@

### Api

@@members test/Boilerplate.Tests/TestSupport/Api/ActionExecutingContextBuilder.cs::ActionExecutingContextBuilder@@

## The kernel tests

Every project has them; each is written out in `testing-kernel.md`.

| Class | File |
|---|---|
| `AggregateRootTests`, `EventContextTests`, `EventHandlerExecutorTests` | `Shared/Domain/` |
| `CommandPipelineTests`, `CommandDispatcherTests`, `ContextCommandTests`, `CommandContextTests` | `Shared/Application/` |
| `AsyncEventHandlerDispatcherTests` | `Infrastructure/Messaging/` |
| `ContextCommandFilterTests` | `Api/Filters/` |
| `CommandScenarioTests` | `Console/` |

## Namespaces and usings

Every file of this page, with its namespace and the `using` lines it needs (`ImplicitUsings` covers `System`, `System.Linq`, `System.Threading.Tasks` and the like). A file the table does not list follows the namespace rule in `naming.md`.

@@usings testing.md@@
