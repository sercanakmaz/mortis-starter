"""Keeps the three reference solutions in reference/variants/ from drifting apart.

1. A file in two variants is identical in both, unless DIFFERS_BY names it and one of its keys has a different value in the
   two variants' MORTIS.md.
2. A file is in a variant exactly when its manifest entry applies to that variant's MORTIS.md.
Prints "variants agree" or each problem, and exits 1 on any problem.
"""
import filecmp
import json
import sys
from itertools import combinations
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
VARIANTS = ROOT / "reference" / "variants"
PRESETS = ("lean", "standard", "advanced")
SCRIPTS = ROOT / "skills" / "mortis-scaffold" / "scripts"
MANIFEST = ROOT / "skills" / "mortis-scaffold" / "references" / "csharp" / "manifest.json"
IGNORED_PARTS = {"bin", "obj", ".mortis", "Migrations"}
DIFFERS_BY = {
    "test/Boilerplate.Tests/Infrastructure/Persistence/Products/ProductRepositoryTests.cs": ["addons"],
    "test/Boilerplate.Tests/Infrastructure/Persistence/Orders/OrderRepositoryTests.cs": ["addons"],
    "test/Boilerplate.Tests/Domain/Orders/OrderTests.cs": ["addons"],
    "test/Boilerplate.Tests/Application/Products/IncreaseStockWhenOrderLineCancelledEventConsumerTests.cs": ["addons"],
    "test/Boilerplate.Tests/Application/Products/DecreaseStockWhenOrderLineAddedEventConsumerTests.cs": ["addons"],
    "test/Boilerplate.Tests/Application/Orders/CompleteOrderCommandHandlerTests.cs": ["addons"],
    "test/Boilerplate.Tests/Application/Orders/CancelOrderWhenOrderLineCancelledEventHandlerTests.cs": ["addons"],
    "test/Boilerplate.Tests/Application/Orders/CancelOrderWhenAllLinesCancelledCommandHandlerTests.cs": ["addons"],
    "test/Boilerplate.Tests/Application/Orders/CancelOrderLineCommandHandlerTests.cs": ["addons"],
    "test/Boilerplate.Tests/Application/Orders/AddOrderLineCommandHandlerTests.cs": ["addons"],
    "test/Boilerplate.Tests/Application/Customers/RegisterCompletedOrderWhenOrderCompletedEventHandlerTests.cs": ["addons"],
    "src/Boilerplate.Domain/Boilerplate.Domain.csproj": ["addons"],
    "src/Boilerplate.Application/Products/IncreaseStockWhenOrderLineCancelledEventConsumer.cs": ["addons"],
    "src/Boilerplate.Application/Products/DecreaseStockWhenOrderLineAddedEventConsumer.cs": ["addons"],
    "src/Boilerplate.Application/Orders/CancelOrderWhenOrderLineCancelledEventHandler.cs": ["addons"],
    "src/Boilerplate.Application/Customers/RegisterCompletedOrderWhenOrderCompletedEventHandler.cs": ["addons"],
    "src/Boilerplate.Console/appsettings.Development.json": ["addons"],
    "src/Boilerplate.Api/appsettings.Development.json": ["addons"],
    "test/Boilerplate.Tests/TestSupport/Domain/Orders/OrderRepositoryBuilder.cs": ["addons"],
    "src/Boilerplate.Infrastructure/Persistence/Orders/OrderRepository.cs": ["addons"],
    "src/Boilerplate.Domain/Aggregates/Orders/OrderConstants.cs": ["addons"],
    "src/Boilerplate.Domain/Aggregates/Orders/IOrderRepository.cs": ["addons"],
    "test/Boilerplate.Tests/TestSupport/Scenarios/ScenarioHost.cs": ["addons"],
    "src/Boilerplate.Infrastructure/Persistence/Products/ProductRepository.cs": ["addons"],
    "src/Boilerplate.Domain/Aggregates/Products/IProductRepository.cs": ["addons"],
    "src/Boilerplate.Infrastructure/Logging/LoggingConstants.cs": ["addons"],
    "src/Boilerplate.Infrastructure/Logging/JsonLineFormatter.cs": ["addons"],
    "src/Boilerplate.Shared/Domain/EventHandlerExecutor.cs": ["addons"],
    "src/Boilerplate.Worker/appsettings.json": ["addons"],
    "src/Boilerplate.Worker/Program.cs": ["addons"],
    "test/Boilerplate.Tests/Architecture/event-contracts.json": ["addons"],
    "test/Boilerplate.Tests/Shared/Domain/EventContextTests.cs": ["addons"],
    "test/Boilerplate.Tests/Shared/Application/CommandPipelineTests.cs": ["addons"],
    "test/Boilerplate.Tests/Shared/Application/CommandContextTests.cs": ["addons"],
    "src/Boilerplate.Infrastructure/Localization/messages.tr.json": ["addons"],
    "src/Boilerplate.Infrastructure/Localization/messages.en.json": ["addons"],
    "src/Boilerplate.Infrastructure/Logging/LogFields.cs": ["addons"],
    "src/Boilerplate.Shared/Application/CommandPipeline.cs": ["addons"],
    "src/Boilerplate.Shared/Domain/EventContext.cs": ["addons"],
    "src/Boilerplate.Shared/Domain/EventBase.cs": ["addons"],
    "src/Boilerplate.Shared/Application/CommandContext.cs": ["addons"],
    "test/Boilerplate.IntegrationTests/TestSupport/OutboxRows.cs": ["events.transport"],
    "test/Boilerplate.IntegrationTests/TestSupport/PostgresFixture.cs": ["tenancy.mode", "languages.mode", "addons"],
    "test/Boilerplate.IntegrationTests/TestSupport/IntegrationServices.cs": ["tenancy.mode", "events.transport"],
    "test/Boilerplate.IntegrationTests/Persistence/UnitOfWorkTests.cs": ["tenancy.mode", "languages.mode"],
    "test/Boilerplate.IntegrationTests/Persistence/ProductRepositoryTests.cs": ["tenancy.mode", "languages.mode"],
    "test/Boilerplate.IntegrationTests/Boilerplate.IntegrationTests.csproj": ["testing", "events.transport"],
    "docs/adr/0023-testing-levels.md": ["testing"],
    "test/Boilerplate.Tests/Boilerplate.Tests.csproj": ["testing"],
    "test/Boilerplate.Tests/Infrastructure/Persistence/Products/ProductReaderTests.cs": ["languages.mode"],
    "test/Boilerplate.Tests/Application/Products/ListProductsQueryHandlerTests.cs": ["languages.mode"],
    "test/Boilerplate.Tests/Application/Products/GetProductQueryHandlerTests.cs": ["languages.mode", "addons"],
    "src/Boilerplate.Infrastructure/Persistence/Products/ProductReader.cs": ["languages.mode"],
    "src/Boilerplate.Domain/Aggregates/Products/ProductView.cs": ["languages.mode"],
    "src/Boilerplate.Application/Products/ListProductsQuery.cs": ["languages.mode"],
    "src/Boilerplate.Application/Products/GetProductQuery.cs": ["languages.mode", "addons"],
    "src/Boilerplate.Infrastructure/Persistence/Customers/CustomerReader.cs": ["addons"],
    "src/Boilerplate.Domain/Aggregates/Customers/CustomerView.cs": ["addons"],
    "src/Boilerplate.Domain/Aggregates/Customers/CustomerConstants.cs": ["addons"],
    "src/Boilerplate.Application/Customers/CreateCustomerCommand.cs": ["addons"],
    "src/Boilerplate.Api/Middlewares/ApiConstants.cs": ["addons"],
    "src/Boilerplate.Api/Controllers/CategoriesController.cs": ["queries"],
    "src/Boilerplate.Api/Controllers/CustomersController.cs": ["queries", "addons"],
    "src/Boilerplate.Api/Controllers/OrdersController.cs": ["queries"],
    "src/Boilerplate.Application/Categories/CategoryDto.cs": ["queries"],
    "src/Boilerplate.Application/Customers/CustomerDto.cs": ["queries", "addons"],
    "src/Boilerplate.Application/Orders/OrderDto.cs": ["queries"],
    "src/Boilerplate.Infrastructure/Persistence/PersistenceModule.cs": ["queries"],
    "src/Boilerplate.Shared/SharedModule.cs": ["queries"],
    "MORTIS.md": ["preset"],
    "Boilerplate.slnx": ["hosts.worker"],
    "Directory.Packages.props": ["events.transport", "auth", "addons"],
    "docker-compose.yml": ["events.transport"],
    "src/Boilerplate.Infrastructure/Boilerplate.Infrastructure.csproj": ["events.transport", "auth", "addons"],
    "src/Boilerplate.Api/appsettings.json": ["events.transport", "auth", "tenancy.mode", "addons"],
    "src/Boilerplate.Console/appsettings.json": ["events.transport", "auth", "tenancy.mode"],
    "docs/architecture.md": ["events.outbox", "events.transport"],
    "docs/adr/0011-outbox-and-publishing-models.md": ["events.outbox"],
    "src/Boilerplate.Api/Program.cs": ["hosts.worker", "addons", "events.transport", "auth"],
    "src/Boilerplate.Console/Program.cs": ["events.transport", "auth", "tenancy.mode", "queries"],
    "src/Boilerplate.Infrastructure/Messaging/EventConsumerModule.cs": ["events.transport"],
    "test/Boilerplate.Tests/Infrastructure/Messaging/EventConsumerModuleTests.cs": ["events.transport"],
    "src/Boilerplate.Infrastructure/Messaging/Outbox/OutboxSettings.cs": ["events.transport"],
    "src/Boilerplate.Infrastructure/InfrastructureModule.cs": ["events.outbox", "events.transport", "auth", "tenancy.mode", "queries", "addons"],
    "src/Boilerplate.Infrastructure/Persistence/UnitOfWork.cs": ["events.outbox", "exceptions"],
    "src/Boilerplate.Infrastructure/Messaging/Outbox/OutboxRepository.cs": ["events.outbox", "events.transport"],
    "src/Boilerplate.Infrastructure/Messaging/Outbox/OutboxMessage.cs": ["events.transport"],
    "src/Boilerplate.Infrastructure/Messaging/Outbox/OutboxMessageConfiguration.cs": ["events.transport"],
    "test/Boilerplate.Tests/Infrastructure/Messaging/Outbox/OutboxMessageTests.cs": ["events.transport"],
    "src/Boilerplate.Infrastructure/Messaging/Outbox/OutboxWorker.cs": ["events.outbox", "events.transport"],
    "src/Boilerplate.Infrastructure/Messaging/Outbox/OutboxModule.cs": ["events.outbox"],
    "src/Boilerplate.Api/Boilerplate.Api.csproj": ["auth", "addons"],
    "src/Boilerplate.Console/Boilerplate.Console.csproj": ["auth"],
    "src/Boilerplate.Api/Filters/ContextCommandFilter.cs": ["auth", "tenancy.mode", "queries"],
    "test/Boilerplate.Tests/Api/Filters/ContextCommandFilterTests.cs": ["auth", "tenancy.mode"],
    "test/Boilerplate.Tests/TestSupport/Api/ActionExecutingContextBuilder.cs": ["auth", "tenancy.mode"],
    "src/Boilerplate.Console/ConsoleCommandRunner.cs": ["auth", "queries"],
    "src/Boilerplate.Console/ConsoleConstants.cs": ["auth", "queries"],
    "test/Boilerplate.Tests/Console/ConsoleCommandRunnerTests.cs": ["auth"],
    "src/Boilerplate.Console/ConsoleTokens.cs": ["tenancy.mode"],
    "test/Boilerplate.Tests/Console/ConsoleTokensTests.cs": ["tenancy.mode"],
    "src/Boilerplate.Infrastructure/Authentication/AuthSettings.cs": ["tenancy.mode"],
    "test/Boilerplate.Tests/Infrastructure/Authentication/AuthSettingsTests.cs": ["tenancy.mode"],
    "src/Boilerplate.Domain/Aggregates/Customers/Customer.cs": ["tenancy.mode", "addons"],
    "src/Boilerplate.Domain/Aggregates/Orders/Order.cs": ["tenancy.mode"],
    "src/Boilerplate.Domain/Aggregates/Orders/OrderLine.cs": ["tenancy.mode"],
    "src/Boilerplate.Domain/Aggregates/Products/Product.cs": ["tenancy.mode", "addons"],
    "src/Boilerplate.Infrastructure/Persistence/AppDbContext.cs": ["tenancy.mode", "addons"],
    "src/Boilerplate.Infrastructure/Persistence/Customers/CustomerConfiguration.cs": ["tenancy.mode", "addons"],
    "src/Boilerplate.Infrastructure/Persistence/DesignTimeDbContextFactory.cs": ["tenancy.mode", "addons"],
    "src/Boilerplate.Infrastructure/Persistence/Orders/OrderConfiguration.cs": ["tenancy.mode"],
    "src/Boilerplate.Infrastructure/Persistence/PersistenceConstants.cs": ["tenancy.mode", "addons"],
    "src/Boilerplate.Infrastructure/Persistence/Products/ProductConfiguration.cs": ["tenancy.mode", "languages.mode"],
    "test/Boilerplate.Tests/Infrastructure/Persistence/Orders/OrderConfigurationTests.cs": ["tenancy.mode"],
    "test/Boilerplate.Tests/Infrastructure/Persistence/Products/ProductConfigurationTests.cs": ["tenancy.mode", "addons"],
    "test/Boilerplate.Tests/TestSupport/Infrastructure/Persistence/AppDbContextBuilder.cs": ["tenancy.mode", "addons"],
    "src/Boilerplate.Domain/ErrorCatalogue.cs": ["tenancy.mode", "addons"],
    "src/Boilerplate.Api/Controllers/ProductsController.cs": ["languages.mode", "queries", "addons"],
    "src/Boilerplate.Api/Middlewares/ExceptionHandler.cs": ["languages.mode"],
    "src/Boilerplate.Application/ApplicationModule.cs": ["languages.mode", "queries", "addons"],
    "src/Boilerplate.Application/Products/CreateProductCommand.cs": ["languages.mode"],
    "src/Boilerplate.Application/Products/DecreaseProductStockCommand.cs": ["languages.mode"],
    "src/Boilerplate.Application/Products/GetProductCommand.cs": ["languages.mode"],
    "src/Boilerplate.Application/Products/IncreaseProductStockCommand.cs": ["languages.mode"],
    "src/Boilerplate.Application/Products/ProductDto.cs": ["languages.mode", "queries"],
    "src/Boilerplate.Shared/Exceptions/DuplicateException.cs": ["languages.mode"],
    "src/Boilerplate.Shared/Exceptions/ExceptionBase.cs": ["languages.mode"],
    "src/Boilerplate.Shared/Exceptions/ForbiddenException.cs": ["languages.mode"],
    "src/Boilerplate.Shared/Exceptions/InvalidParameterException.cs": ["languages.mode"],
    "src/Boilerplate.Shared/Exceptions/NotFoundException.cs": ["languages.mode"],
    "src/Boilerplate.Shared/Exceptions/ReferenceViolationException.cs": ["languages.mode"],
    "src/Boilerplate.Shared/Exceptions/TransactionRequiredException.cs": ["languages.mode"],
    "test/Boilerplate.Tests/Api/Middlewares/ExceptionHandlerTests.cs": ["languages.mode"],
    "test/Boilerplate.Tests/Application/Products/CreateProductCommandHandlerTests.cs": ["languages.mode"],
    "test/Boilerplate.Tests/Domain/Products/ProductTests.cs": ["languages.mode", "addons"],
    "test/Boilerplate.Tests/TestSupport/Domain/Products/FakeProduct.cs": ["languages.mode"],
    "test/Boilerplate.Tests/TestSupport/Domain/Products/ProductRepositoryBuilder.cs": ["languages.mode", "addons"],
}

sys.path.insert(0, str(SCRIPTS))
from decisions import load  # noqa: E402
from structure import applies  # noqa: E402


def files_of(preset):
    root = VARIANTS / preset
    return {str(p.relative_to(root)) for p in root.rglob("*") if p.is_file() and not (IGNORED_PARTS & set(p.relative_to(root).parts))}


def main():
    if not VARIANTS.is_dir():
        print("no variants to check")
        return 0
    problems = []
    values = {preset: load(VARIANTS / preset / "MORTIS.md")[2] for preset in PRESETS}
    present = {preset: files_of(preset) for preset in PRESETS}
    for first, second in combinations(PRESETS, 2):
        for relative in sorted(present[first] & present[second]):
            if filecmp.cmp(VARIANTS / first / relative, VARIANTS / second / relative, shallow=False):
                continue
            keys = DIFFERS_BY.get(relative, [])
            if not any(values[first].get(key) != values[second].get(key) for key in keys):
                problems.append(f"{relative} differs between {first} and {second}, and no option explains it")
    for entry in [e for e in json.loads(MANIFEST.read_text())["files"] if not e.get("unbuilt")]:
        if entry.get("glob") or entry["path"] == "MORTIS.md" or "/Migrations/" in entry["path"]:
            continue
        relative = entry["path"].replace("{Name}", "Boilerplate")
        for preset in PRESETS:
            expected = applies(entry, values[preset])
            if expected != (relative in present[preset]):
                problems.append(f"{relative}: the manifest says {'present' if expected else 'absent'} for {preset}, the variant disagrees")
    for problem in problems:
        print(problem)
    print("variants agree" if not problems else f"{len(problems)} problem(s)")
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main())
