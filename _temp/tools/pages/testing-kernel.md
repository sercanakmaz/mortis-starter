# The kernel tests, C#

Every project has these tests, written exactly so. Each class lives in the folder of the type it tests (`testing.md`). Each test method is shown in order.

## Shared/Domain

@@members test/Boilerplate.Tests/Shared/Domain/AggregateRootTests.cs::AggregateRootTests@@

@@members test/Boilerplate.Tests/Shared/Domain/EventContextTests.cs::EventContextTests@@

@@members test/Boilerplate.Tests/Shared/Domain/EventHandlerExecutorTests.cs::EventHandlerExecutorTests@@

## Shared/Application

@@members test/Boilerplate.Tests/Shared/Application/CommandPipelineTests.cs::CommandPipelineTests@@

@@members test/Boilerplate.Tests/Shared/Application/CommandDispatcherTests.cs::CommandDispatcherTests@@

@@members test/Boilerplate.Tests/Shared/Application/UserCommandTests.cs::UserCommandTests@@

## Infrastructure/Messaging

@@members test/Boilerplate.Tests/Infrastructure/Messaging/AsyncEventHandlerDispatcherTests.cs::AsyncEventHandlerDispatcherTests@@

## Api/Filters

@@members test/Boilerplate.Tests/Api/Filters/UserCommandFilterTests.cs::UserCommandFilterTests@@

## Console

@@members test/Boilerplate.Tests/Console/CommandScenarioTests.cs::CommandScenarioTests@@

## Namespaces and usings

Every file of this page, with its namespace and the `using` lines it needs (`ImplicitUsings` covers `System`, `System.Linq`, `System.Threading.Tasks` and the like). A file the table does not list follows the namespace rule in `naming.md`.

@@usings testing-kernel.md@@
