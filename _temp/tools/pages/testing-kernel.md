# The kernel tests, C#

Every project has these tests, written exactly so. Each class lives in the folder of the type it tests (`testing.md`). Each test method is shown in order.

## Shared/Domain

@@members test/Boilerplate.Tests/Shared/Domain/AggregateRootTests.cs::AggregateRootTests@@

@@members test/Boilerplate.Tests/Shared/Domain/EventContextTests.cs::EventContextTests@@

@@members test/Boilerplate.Tests/Shared/Domain/EventHandlerExecutorTests.cs::EventHandlerExecutorTests@@

## Shared/Application

@@members test/Boilerplate.Tests/Shared/Application/CommandPipelineTests.cs::CommandPipelineTests@@

@@members test/Boilerplate.Tests/Shared/Application/CommandDispatcherTests.cs::CommandDispatcherTests@@

@@members test/Boilerplate.Tests/Shared/Application/ContextCommandTests.cs::ContextCommandTests@@

@@members test/Boilerplate.Tests/Shared/Application/CommandContextTests.cs::CommandContextTests@@

## Infrastructure/Messaging

`EventPublisherTests` (only when `events.outbox` is `none`) are on `events-inprocess.md`; the outbox's tests on `outbox.md`.

## Infrastructure/Logging

@@members test/Boilerplate.Tests/Infrastructure/Logging/JsonLineFormatterTests.cs::JsonLineFormatterTests@@

## Api/Middlewares

@@members test/Boilerplate.Tests/Api/Middlewares/ExceptionHandlerTests.cs::ExceptionHandlerTests@@

## Api/Filters

@@members test/Boilerplate.Tests/Api/Filters/ContextCommandFilterTests.cs::ContextCommandFilterTests@@

## Console

@@members test/Boilerplate.Tests/Console/ConsoleCommandRunnerTests.cs::ConsoleCommandRunnerTests@@

## Namespaces and usings

Every file of this page, with its namespace and the `using` lines it needs (`ImplicitUsings` covers `System`, `System.Linq`, `System.Threading.Tasks` and the like). A file the table does not list follows the namespace rule in `naming.md`.

@@usings testing-kernel.md@@
