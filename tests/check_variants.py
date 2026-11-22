"""Keeps the three reference solutions in _temp/variants/ from drifting apart.

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
VARIANTS = ROOT / "_temp" / "variants"
PRESETS = ("lean", "standard", "advanced")
SCRIPTS = ROOT / "skills" / "mortis-scaffold" / "scripts"
MANIFEST = ROOT / "skills" / "mortis-scaffold" / "references" / "csharp" / "manifest.json"
IGNORED_PARTS = {"bin", "obj", ".mortis", "Migrations"}
DIFFERS_BY = {
    "MORTIS.md": ["preset"],
    "Boilerplate.slnx": ["hosts.worker"],
    "Directory.Packages.props": ["events.transport", "auth"],
    "docker-compose.yml": ["events.transport"],
    "src/Boilerplate.Infrastructure/Boilerplate.Infrastructure.csproj": ["events.transport", "auth"],
    "src/Boilerplate.Api/appsettings.json": ["events.transport", "auth", "tenancy.mode"],
    "src/Boilerplate.Console/appsettings.json": ["events.transport", "auth", "tenancy.mode"],
    "docs/architecture.md": ["events.outbox", "events.transport"],
    "docs/adr/0011-outbox-and-publishing-models.md": ["events.outbox"],
    "src/Boilerplate.Api/Program.cs": ["hosts.worker", "addons", "events.transport", "auth"],
    "src/Boilerplate.Console/Program.cs": ["events.transport", "auth", "tenancy.mode"],
    "src/Boilerplate.Infrastructure/Messaging/EventConsumerModule.cs": ["events.transport"],
    "test/Boilerplate.Tests/Infrastructure/Messaging/EventConsumerModuleTests.cs": ["events.transport"],
    "src/Boilerplate.Infrastructure/Messaging/Outbox/OutboxSettings.cs": ["events.transport"],
    "src/Boilerplate.Infrastructure/InfrastructureModule.cs": ["events.outbox", "events.transport", "auth", "tenancy.mode"],
    "src/Boilerplate.Infrastructure/Persistence/UnitOfWork.cs": ["events.outbox", "exceptions"],
    "src/Boilerplate.Infrastructure/Messaging/Outbox/OutboxRepository.cs": ["events.outbox", "events.transport"],
    "src/Boilerplate.Infrastructure/Messaging/Outbox/OutboxMessage.cs": ["events.transport"],
    "src/Boilerplate.Infrastructure/Messaging/Outbox/OutboxMessageConfiguration.cs": ["events.transport"],
    "test/Boilerplate.Tests/Infrastructure/Messaging/Outbox/OutboxMessageTests.cs": ["events.transport"],
    "src/Boilerplate.Infrastructure/Messaging/Outbox/OutboxWorker.cs": ["events.outbox", "events.transport"],
    "src/Boilerplate.Infrastructure/Messaging/Outbox/OutboxModule.cs": ["events.outbox"],
    "src/Boilerplate.Api/Boilerplate.Api.csproj": ["auth"],
    "src/Boilerplate.Console/Boilerplate.Console.csproj": ["auth"],
    "src/Boilerplate.Api/Filters/ContextCommandFilter.cs": ["auth", "tenancy.mode"],
    "test/Boilerplate.Tests/Api/Filters/ContextCommandFilterTests.cs": ["auth", "tenancy.mode"],
    "test/Boilerplate.Tests/TestSupport/Api/ActionExecutingContextBuilder.cs": ["auth", "tenancy.mode"],
    "src/Boilerplate.Console/ConsoleCommandRunner.cs": ["auth"],
    "src/Boilerplate.Console/ConsoleConstants.cs": ["auth"],
    "test/Boilerplate.Tests/Console/ConsoleCommandRunnerTests.cs": ["auth"],
    "src/Boilerplate.Console/ConsoleTokens.cs": ["tenancy.mode"],
    "test/Boilerplate.Tests/Console/ConsoleTokensTests.cs": ["tenancy.mode"],
    "src/Boilerplate.Infrastructure/Authentication/AuthSettings.cs": ["tenancy.mode"],
    "test/Boilerplate.Tests/Infrastructure/Authentication/AuthSettingsTests.cs": ["tenancy.mode"],
    "src/Boilerplate.Domain/Aggregates/Customers/Customer.cs": ["tenancy.mode"],
    "src/Boilerplate.Domain/Aggregates/Orders/Order.cs": ["tenancy.mode"],
    "src/Boilerplate.Domain/Aggregates/Orders/OrderLine.cs": ["tenancy.mode"],
    "src/Boilerplate.Domain/Aggregates/Products/Product.cs": ["tenancy.mode"],
    "src/Boilerplate.Infrastructure/Persistence/AppDbContext.cs": ["tenancy.mode"],
    "src/Boilerplate.Infrastructure/Persistence/Customers/CustomerConfiguration.cs": ["tenancy.mode"],
    "src/Boilerplate.Infrastructure/Persistence/DesignTimeDbContextFactory.cs": ["tenancy.mode"],
    "src/Boilerplate.Infrastructure/Persistence/Orders/OrderConfiguration.cs": ["tenancy.mode"],
    "src/Boilerplate.Infrastructure/Persistence/PersistenceConstants.cs": ["tenancy.mode"],
    "src/Boilerplate.Infrastructure/Persistence/Products/ProductConfiguration.cs": ["tenancy.mode"],
    "test/Boilerplate.Tests/Infrastructure/Persistence/Orders/OrderConfigurationTests.cs": ["tenancy.mode"],
    "test/Boilerplate.Tests/Infrastructure/Persistence/Products/ProductConfigurationTests.cs": ["tenancy.mode"],
    "test/Boilerplate.Tests/TestSupport/Infrastructure/Persistence/AppDbContextBuilder.cs": ["tenancy.mode"],
    "src/Boilerplate.Domain/ErrorCatalogue.cs": ["tenancy.mode"],
    "src/Boilerplate.Api/Controllers/ProductsController.cs": ["languages.mode"],
    "src/Boilerplate.Api/Middlewares/ExceptionHandler.cs": ["languages.mode"],
    "src/Boilerplate.Application/ApplicationModule.cs": ["languages.mode"],
    "src/Boilerplate.Application/Products/CreateProductCommand.cs": ["languages.mode"],
    "src/Boilerplate.Application/Products/DecreaseProductStockCommand.cs": ["languages.mode"],
    "src/Boilerplate.Application/Products/GetProductCommand.cs": ["languages.mode"],
    "src/Boilerplate.Application/Products/IncreaseProductStockCommand.cs": ["languages.mode"],
    "src/Boilerplate.Application/Products/ProductDto.cs": ["languages.mode"],
    "src/Boilerplate.Shared/Exceptions/DuplicateException.cs": ["languages.mode"],
    "src/Boilerplate.Shared/Exceptions/ExceptionBase.cs": ["languages.mode"],
    "src/Boilerplate.Shared/Exceptions/ForbiddenException.cs": ["languages.mode"],
    "src/Boilerplate.Shared/Exceptions/InvalidParameterException.cs": ["languages.mode"],
    "src/Boilerplate.Shared/Exceptions/NotFoundException.cs": ["languages.mode"],
    "src/Boilerplate.Shared/Exceptions/ReferenceViolationException.cs": ["languages.mode"],
    "src/Boilerplate.Shared/Exceptions/TransactionRequiredException.cs": ["languages.mode"],
    "test/Boilerplate.Tests/Api/Middlewares/ExceptionHandlerTests.cs": ["languages.mode"],
    "test/Boilerplate.Tests/Application/Products/CreateProductCommandHandlerTests.cs": ["languages.mode"],
    "test/Boilerplate.Tests/Domain/Products/ProductTests.cs": ["languages.mode"],
    "test/Boilerplate.Tests/TestSupport/Domain/Products/FakeProduct.cs": ["languages.mode"],
    "test/Boilerplate.Tests/TestSupport/Domain/Products/ProductRepositoryBuilder.cs": ["languages.mode"],
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
