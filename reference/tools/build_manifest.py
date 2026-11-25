import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
VARIANTS = ROOT / "reference" / "variants"
PRESETS = ("lean", "standard", "advanced")
TEMPLATE = ROOT / "reference" / "tools"
TARGET = ROOT / "skills" / "mortis-scaffold" / "references" / "csharp" / "manifest.json"
OUTBOX = ["after-command", "background"]
OPTION_FILES = {
    "src/Boilerplate.Api/Middlewares/CorrelationIdMiddleware.cs": {"addons": "correlation"},
    "test/Boilerplate.Tests/Api/Middlewares/CorrelationIdMiddlewareTests.cs": {"addons": "correlation"},
    "docs/adr/0009-correlation-header.md": {"addons": "correlation"},
    "src/Boilerplate.Infrastructure/Persistence/DatabaseExceptionHandler.cs": {"exceptions": ["coded", "catalogued"]},
    "src/Boilerplate.Domain/ErrorCatalogue.cs": {"exceptions": "catalogued"},
    "test/Boilerplate.Tests/Domain/ErrorCatalogueTests.cs": {"exceptions": "catalogued"},
    "src/Boilerplate.Infrastructure/Messaging/EventPublisher.cs": {"events.outbox": "none"},
    "src/Boilerplate.Infrastructure/Messaging/MessagingConstants.cs": {"events.outbox": "none"},
    "test/Boilerplate.Tests/Infrastructure/Messaging/EventPublisherTests.cs": {"events.outbox": "none"},
    "test/Boilerplate.Tests/TestSupport/Infrastructure/Messaging/EventPublisherBuilder.cs": {"events.outbox": "none"},
    "docs/adr/0011-outbox-and-publishing-models.md": {"events.outbox": OUTBOX},
    "docs/adr/0013-own-bus-compatible-with-masstransit.md": {"events.transport": "rabbitmq"},
    "docs/adr/0014-bearer-tokens-fill-the-context.md": {"auth": "jwt"},
    "docs/adr/0015-multi-tenancy-by-interface.md": {"tenancy.mode": "interface"},
    "docs/adr/0016-languages-of-messages-and-data.md": {"languages.mode": ["messages", "data"]},
    "src/Boilerplate.Worker/Boilerplate.Worker.csproj": {"hosts.worker": "separate"},
    "src/Boilerplate.Worker/Program.cs": {"hosts.worker": "separate"},
    "src/Boilerplate.Worker/appsettings.json": {"hosts.worker": "separate"},
    "src/Boilerplate.Infrastructure/Messaging/EventConsumerRegistry.cs": {"events.transport": ["in-process", "database"]},
}
AUTH_PREFIXES = ("src/Boilerplate.Infrastructure/Authentication/", "src/Boilerplate.Api/Authentication/", "src/Boilerplate.Console/ConsoleTokens.cs",
                 "src/Boilerplate.Api/appsettings.Development.json", "src/Boilerplate.Console/appsettings.Development.json",
                 "test/Boilerplate.Tests/Infrastructure/Authentication/", "test/Boilerplate.Tests/TestSupport/Infrastructure/Authentication/", "test/Boilerplate.Tests/Console/ConsoleTokensTests.cs")
TENANCY_PREFIXES = ("src/Boilerplate.Shared/Domain/IMultiTenant.cs", "src/Boilerplate.Shared/Exceptions/TenantRequiredException.cs", "src/Boilerplate.Infrastructure/Tenancy/",
                    "test/Boilerplate.Tests/Infrastructure/Tenancy/", "test/Boilerplate.Tests/TestSupport/Infrastructure/Tenancy/", "test/Boilerplate.Tests/Infrastructure/Persistence/AppDbContextTests.cs")
MESSAGES_PREFIXES = ("src/Boilerplate.Shared/Application/Languages.cs", "src/Boilerplate.Infrastructure/Localization/", "src/Boilerplate.Api/Middlewares/RequestLanguage.cs",
                     "test/Boilerplate.Tests/Infrastructure/Localization/", "test/Boilerplate.Tests/Shared/Application/LanguagesTests.cs")
DATA_PREFIXES = ("src/Boilerplate.Shared/Domain/LocalizedText.cs", "src/Boilerplate.Infrastructure/Persistence/LocalizedTextConversion.cs", "test/Boilerplate.Tests/Shared/Domain/LocalizedTextTests.cs")
DATA_EXAMPLE_FILES = ("src/Boilerplate.Application/Products/RenameProductCommand.cs", "test/Boilerplate.Tests/Application/Products/RenameProductCommandHandlerTests.cs")
POSTGRES_PREFIXES = ("src/Boilerplate.Infrastructure/Persistence/", "test/Boilerplate.Tests/Infrastructure/Persistence/", "test/Boilerplate.Tests/TestSupport/Infrastructure/Persistence/", "src/Boilerplate.Infrastructure/Messaging/Outbox/OutboxMessageConfiguration.cs")
POSTGRES_EXCEPTIONS = ("src/Boilerplate.Infrastructure/Persistence/PersistenceConstants.cs",)
MONGO_FILES = {
    "src/{Name}.Infrastructure/Persistence/Mongo/MongoModule.cs": ({}, True),
    "src/{Name}.Infrastructure/Persistence/Mongo/MongoClassMaps.cs": ({}, True),
    "src/{Name}.Infrastructure/Persistence/Mongo/MongoUnitOfWork.cs": ({}, True),
    "src/{Name}.Infrastructure/Persistence/Mongo/MongoSession.cs": ({}, True),
    "src/{Name}.Infrastructure/Messaging/Outbox/OutboxConstants.cs": ({"events.outbox": ["background", "after-command"]}, True),
    "src/{Name}.Infrastructure/Persistence/Mongo/Categories/CategoryReader.cs": ({"example": "true", "queries": ["split", "read-models"]}, False),
    "src/{Name}.Infrastructure/Persistence/Mongo/Customers/CustomerReader.cs": ({"example": "true", "queries": ["split", "read-models"]}, False),
    "src/{Name}.Infrastructure/Persistence/Mongo/Orders/OrderReader.cs": ({"example": "true", "queries": ["split", "read-models"]}, False),
    "src/{Name}.Infrastructure/Persistence/Mongo/Products/ProductReader.cs": ({"example": "true", "queries": ["split", "read-models"]}, False),
    "test/{Name}.Tests/Infrastructure/Persistence/Mongo/ProductReaderTests.cs": ({"example": "true", "queries": ["split", "read-models"]}, False),
    "src/{Name}.Infrastructure/Persistence/Mongo/MongoRepository.cs": ({}, True),
    "src/{Name}.Infrastructure/Persistence/Mongo/MongoExceptionHandler.cs": ({}, True),
    "src/{Name}.Infrastructure/Persistence/Mongo/ConcurrencyConflictException.cs": ({}, True),
    "src/{Name}.Infrastructure/Persistence/Mongo/MongoIndexes.cs": ({}, True),
    "src/{Name}.Infrastructure/Persistence/Mongo/MongoIndexesService.cs": ({}, True),
    "src/{Name}.Infrastructure/Persistence/Mongo/Categories/CategoryRepository.cs": ({"example": "true"}, False),
    "src/{Name}.Infrastructure/Persistence/Mongo/Customers/CustomerRepository.cs": ({"example": "true"}, False),
    "src/{Name}.Infrastructure/Persistence/Mongo/Orders/OrderRepository.cs": ({"example": "true"}, False),
    "src/{Name}.Infrastructure/Persistence/Mongo/Products/ProductRepository.cs": ({"example": "true"}, False),
    "test/{Name}.Tests/Infrastructure/Persistence/Mongo/MongoClassMapsTests.cs": ({}, False),
    "test/{Name}.Tests/Infrastructure/Persistence/Mongo/MongoRepositoryRulesTests.cs": ({}, False),
    "test/{Name}.Tests/TestSupport/SolutionFolder.cs": ({}, False),
}
QUERY_FILES = {
    "src/{Name}.Shared/Application/IQuery.cs": ({}, True),
    "src/{Name}.Shared/Application/BaseQueryHandler.cs": ({}, True),
    "src/{Name}.Shared/Application/IQueryDispatcher.cs": ({}, True),
    "src/{Name}.Shared/Application/QueryDispatcher.cs": ({}, True),
    "src/{Name}.Shared/Application/QueryHandlerExtensions.cs": ({}, True),
    "src/{Name}.Shared/Application/PageRequest.cs": ({}, True),
    "src/{Name}.Shared/Application/PagedResult.cs": ({}, True),
    "src/{Name}.Domain/Aggregates/Products/IProductReader.cs": ({"example": "true"}, False),
    "src/{Name}.Domain/Aggregates/Products/ProductView.cs": ({"example": "true"}, False),
    "src/{Name}.Domain/Aggregates/Orders/IOrderReader.cs": ({"example": "true"}, False),
    "src/{Name}.Domain/Aggregates/Orders/OrderView.cs": ({"example": "true"}, False),
    "src/{Name}.Domain/Aggregates/Customers/ICustomerReader.cs": ({"example": "true"}, False),
    "src/{Name}.Domain/Aggregates/Customers/CustomerView.cs": ({"example": "true"}, False),
    "src/{Name}.Domain/Aggregates/Categories/ICategoryReader.cs": ({"example": "true"}, False),
    "src/{Name}.Domain/Aggregates/Categories/CategoryView.cs": ({"example": "true"}, False),
    "src/{Name}.Application/Products/GetProductQuery.cs": ({"example": "true"}, False),
    "src/{Name}.Application/Products/ListProductsQuery.cs": ({"example": "true"}, False),
    "src/{Name}.Application/Orders/GetOrderQuery.cs": ({"example": "true"}, False),
    "src/{Name}.Application/Customers/GetCustomerQuery.cs": ({"example": "true"}, False),
    "src/{Name}.Application/Categories/GetCategoryQuery.cs": ({"example": "true"}, False),
    "src/{Name}.Infrastructure/Persistence/Products/ProductReader.cs": ({"example": "true", "database": "postgres"}, False),
    "src/{Name}.Infrastructure/Persistence/Orders/OrderReader.cs": ({"example": "true", "database": "postgres"}, False),
    "src/{Name}.Infrastructure/Persistence/Customers/CustomerReader.cs": ({"example": "true", "database": "postgres"}, False),
    "src/{Name}.Infrastructure/Persistence/Categories/CategoryReader.cs": ({"example": "true", "database": "postgres"}, False),
    "test/{Name}.Tests/Application/QueryRulesTests.cs": ({}, False),
    "test/{Name}.Tests/Infrastructure/Persistence/Products/ProductReaderTests.cs": ({"example": "true", "database": "postgres"}, False),
    "test/{Name}.Tests/Application/Products/GetProductQueryHandlerTests.cs": ({"example": "true"}, False),
    "test/{Name}.Tests/Application/Products/ListProductsQueryHandlerTests.cs": ({"example": "true"}, False),
    "test/{Name}.Tests/TestSupport/Domain/Products/ProductReaderBuilder.cs": ({"example": "true"}, False),
}
READ_MODEL_FILES = {
    "src/{Name}.Infrastructure/ReadModels/ReadModelModule.cs": ({"example": "true"}, False),
    "src/{Name}.Infrastructure/ReadModels/OrderSummaries/OrderSummary.cs": ({"example": "true"}, False),
    "src/{Name}.Infrastructure/ReadModels/OrderSummaries/OrderSummaryConfiguration.cs": ({"example": "true"}, False),
    "src/{Name}.Infrastructure/ReadModels/OrderSummaries/OrderSummaryProjection.cs": ({"example": "true"}, False),
    "src/{Name}.Infrastructure/ReadModels/OrderSummaries/OrderSummaryRefresh.cs": ({"example": "true"}, False),
    "src/{Name}.Infrastructure/ReadModels/OrderSummaries/OrderSummaryReader.cs": ({"example": "true"}, False),
    "src/{Name}.Domain/Aggregates/Orders/IOrderSummaryReader.cs": ({"example": "true"}, False),
    "src/{Name}.Domain/Aggregates/Orders/OrderSummaryView.cs": ({"example": "true"}, False),
    "src/{Name}.Application/Orders/ListOrderSummariesQuery.cs": ({"example": "true"}, False),
    "test/{Name}.Tests/Infrastructure/ReadModels/ReadModelModuleTests.cs": ({"example": "true"}, False),
}
PERMISSION_FILES = {
    "src/{Name}.Shared/Application/IAuthorizedCommand.cs": {},
    "src/{Name}.Infrastructure/Authentication/PermissionsFormat.cs": {},
    "src/{Name}.Application/Products/ProductPermissions.cs": {"example": "true"},
    "src/{Name}.Application/Orders/OrderPermissions.cs": {"example": "true"},
    "src/{Name}.Application/Customers/CustomerPermissions.cs": {"example": "true"},
    "src/{Name}.Application/Categories/CategoryPermissions.cs": {"example": "true"},
    "test/{Name}.Tests/Application/PermissionRulesTests.cs": {},
}
API_CONVENTION_FILES = {
    "src/{Name}.Shared/Application/Optional.cs": ({}, True),
    "src/{Name}.Shared/Application/OptionalJsonConverterFactory.cs": ({}, True),
    "src/{Name}.Shared/Application/OptionalJsonConverter.cs": ({}, True),
    "src/{Name}.Api/Conventions/ApiConventionsModule.cs": ({}, False),
    "src/{Name}.Application/Customers/ChangeCustomerCommand.cs": ({"example": "true"}, False),
    "src/{Name}.Domain/Aggregates/Customers/Events/Changed.cs": ({"example": "true"}, False),
    "test/{Name}.Tests/Shared/Application/OptionalTests.cs": ({}, False),
    "test/{Name}.Tests/TestSupport/Shared/Application/OptionalProbe.cs": ({}, False),
    "test/{Name}.Tests/Api/Conventions/ApiConventionsModuleTests.cs": ({}, False),
    "test/{Name}.Tests/Application/Customers/ChangeCustomerCommandHandlerTests.cs": ({"example": "true"}, False),
}
IDEMPOTENCY_FILES = {
    "src/{Name}.Api/Idempotency/IdempotencyFilter.cs": {},
    "src/{Name}.Api/Idempotency/IdempotencyHash.cs": {},
    "src/{Name}.Api/Idempotency/IdempotencyReplay.cs": {},
    "src/{Name}.Api/Idempotency/IdempotencyKeyReusedException.cs": {},
    "src/{Name}.Api/Idempotency/IdempotencyInProgressException.cs": {},
    "src/{Name}.Infrastructure/Persistence/Idempotency/IdempotencyRecord.cs": {},
    "src/{Name}.Infrastructure/Persistence/Idempotency/IdempotencyRecordConfiguration.cs": {},
    "src/{Name}.Infrastructure/Persistence/Idempotency/IdempotencyConstants.cs": {},
    "src/{Name}.Infrastructure/Persistence/Idempotency/IdempotencySettings.cs": {},
    "src/{Name}.Infrastructure/Persistence/Idempotency/IdempotencyStore.cs": {},
    "src/{Name}.Infrastructure/Persistence/Idempotency/IdempotencyCleanupService.cs": {},
    "src/{Name}.Infrastructure/Persistence/Idempotency/IdempotencyModule.cs": {},
    "test/{Name}.Tests/Api/Idempotency/IdempotencyFilterTests.cs": {},
    "test/{Name}.Tests/TestSupport/Api/Idempotency/ResourceExecutingContextBuilder.cs": {},
    "test/{Name}.Tests/TestSupport/Api/Idempotency/IdempotencyStoreBuilder.cs": {},
    "test/{Name}.IntegrationTests/Persistence/IdempotencyStoreTests.cs": {"testing": ["integration", "architecture"]},
}
OBSERVABILITY_FILES = {
    "src/{Name}.Shared/Diagnostics/Telemetry.cs": ({}, True),
    "src/{Name}.Infrastructure/Diagnostics/ObservabilityModule.cs": ({}, False),
    "src/{Name}.Infrastructure/Diagnostics/ObservabilitySettings.cs": ({}, False),
    "src/{Name}.Infrastructure/Diagnostics/ObservabilityConstants.cs": ({}, False),
    "src/{Name}.Infrastructure/Diagnostics/OutboxLagProbe.cs": ({"events.outbox": OUTBOX}, False),
    "src/{Name}.Infrastructure/Diagnostics/OutboxHealthCheck.cs": ({"events.outbox": OUTBOX}, False),
    "src/{Name}.Infrastructure/Diagnostics/RabbitMqHealthCheck.cs": ({"events.transport": "rabbitmq"}, False),
    "src/{Name}.Api/Middlewares/RequestLoggingMiddleware.cs": ({}, False),
    "src/{Name}.Api/Observability/HealthResponse.cs": ({}, False),
    "test/{Name}.Tests/TestSupport/Shared/Diagnostics/RecordedActivities.cs": ({}, False),
}
HANDLERS_UP = ["handlers", "integration", "architecture"]
INTEGRATION_UP = ["integration", "architecture"]
TESTING_FILES = {
    "test/{Name}.Tests/TestSupport/Scenarios/ScenarioHost.cs": {"testing": HANDLERS_UP},
    "test/{Name}.Tests/Scenarios/CreateProductScenarioTests.cs": {"testing": HANDLERS_UP, "example": "true"},
    "test/{Name}.Tests/Application/Products/CreateProductCommandValidatorTests.cs": {"testing": HANDLERS_UP, "example": "true"},
    "test/{Name}.IntegrationTests/{Name}.IntegrationTests.csproj": {"testing": INTEGRATION_UP},
    "test/{Name}.IntegrationTests/TestSupport/PostgresFixture.cs": {"testing": INTEGRATION_UP, "database": "postgres"},
    "test/{Name}.IntegrationTests/TestSupport/RabbitMqFixture.cs": {"testing": INTEGRATION_UP, "events.transport": "rabbitmq"},
    "test/{Name}.IntegrationTests/TestSupport/IntegrationConstants.cs": {"testing": INTEGRATION_UP},
    "test/{Name}.IntegrationTests/TestSupport/PostgresCollection.cs": {"testing": INTEGRATION_UP, "database": "postgres"},
    "test/{Name}.IntegrationTests/TestSupport/RabbitMqCollection.cs": {"testing": INTEGRATION_UP, "events.transport": "rabbitmq"},
    "test/{Name}.IntegrationTests/TestSupport/IntegrationServices.cs": {"testing": INTEGRATION_UP},
    "test/{Name}.IntegrationTests/Persistence/ProductRepositoryTests.cs": {"testing": INTEGRATION_UP, "example": "true"},
    "test/{Name}.IntegrationTests/Persistence/UnitOfWorkTests.cs": {"testing": INTEGRATION_UP},
    "test/{Name}.IntegrationTests/Messaging/OutboxClaimTests.cs": {"testing": INTEGRATION_UP, "events.outbox": OUTBOX},
    "test/{Name}.IntegrationTests/TestSupport/MongoFixture.cs": {"testing": INTEGRATION_UP, "database": "mongo"},
    "test/{Name}.IntegrationTests/TestSupport/MongoCollection.cs": {"testing": INTEGRATION_UP, "database": "mongo"},
    "test/{Name}.IntegrationTests/TestSupport/OutboxRows.cs": {"testing": INTEGRATION_UP, "events.outbox": OUTBOX, "database": "postgres"},
    "test/{Name}.IntegrationTests/Messaging/RabbitMqRoundTripTests.cs": {"testing": INTEGRATION_UP, "events.transport": "rabbitmq"},
    "test/{Name}.IntegrationTests/TestSupport/RabbitMqDeliveries.cs": {"testing": INTEGRATION_UP, "events.transport": "rabbitmq"},
    "test/{Name}.Tests/Architecture/LayerRulesTests.cs": {"testing": "architecture"},
    "test/{Name}.Tests/Architecture/RegistrationRulesTests.cs": {"testing": "architecture"},
    "test/{Name}.Tests/Architecture/ServiceRulesTests.cs": {"testing": "architecture"},
    "test/{Name}.Tests/Architecture/EventContractTests.cs": {"testing": "architecture"},
    "test/{Name}.Tests/Architecture/event-contracts.json": {"testing": "architecture"},
    "test/{Name}.Tests/TestSupport/Architecture/EventContracts.cs": {"testing": "architecture"},
}
EX = {"example": "true"}
PG = {"database": "postgres"}
ADVANCED_ADDONS = {
    "soft-delete.md": ({"addons": "soft-delete"}, "docs/adr/0024-soft-delete.md", {
        "src/{Name}.Shared/Domain/ISoftDeletable.cs": ({}, True),
        "src/{Name}.Domain/Aggregates/Products/Events/Deleted.cs": (EX, False),
        "src/{Name}.Domain/Aggregates/Products/Events/Restored.cs": (EX, False),
        "src/{Name}.Application/Products/DeleteProductCommand.cs": (EX, False),
        "src/{Name}.Application/Products/RestoreProductCommand.cs": (EX, False),
        "test/{Name}.Tests/Application/Products/DeleteProductCommandHandlerTests.cs": (EX, False),
        "test/{Name}.Tests/Application/Products/RestoreProductCommandHandlerTests.cs": (EX, False),
    }),
    "caching.md": ({"addons": "caching"}, "docs/adr/0025-caching-reads.md", {
        "src/{Name}.Shared/Application/ICacheService.cs": ({}, True),
        "src/{Name}.Infrastructure/Caching/HybridCacheService.cs": ({}, False),
        "src/{Name}.Infrastructure/Caching/CachingModule.cs": ({}, False),
        "src/{Name}.Infrastructure/Caching/CachingSettings.cs": ({}, False),
        "src/{Name}.Application/Products/ProductCacheKeys.cs": (EX, False),
        "src/{Name}.Application/Products/InvalidateProductCacheWhenStockDecreasedEventHandler.cs": (EX, False),
        "src/{Name}.Application/Products/InvalidateProductCacheWhenStockIncreasedEventHandler.cs": (EX, False),
        "src/{Name}.Application/Products/InvalidateProductCacheWhenRenamedEventHandler.cs": (EX, False),
        "src/{Name}.Application/Products/InvalidateProductCacheWhenDeletedEventHandler.cs": ({**EX, "addons": ["caching", "soft-delete"]}, False),
        "src/{Name}.Application/Products/InvalidateProductCacheWhenRestoredEventHandler.cs": ({**EX, "addons": ["caching", "soft-delete"]}, False),
        "test/{Name}.Tests/Application/Products/InvalidateProductCacheEventHandlerTests.cs": (EX, False),
        "src/{Name}.Domain/Aggregates/Products/Events/Renamed.cs": (EX, False),
        "test/{Name}.Tests/Infrastructure/Caching/HybridCacheServiceTests.cs": ({}, False),
        "test/{Name}.Tests/TestSupport/Shared/Application/PassThroughCacheService.cs": ({}, False),
    }),
    "scheduler.md": ({"addons": "scheduler", "database": "postgres"}, "docs/adr/0026-scheduled-jobs.md", {
        "src/{Name}.Shared/Application/IRecurringJob.cs": ({}, True),
        "src/{Name}.Application/Orders/CancelEmptyOrdersJob.cs": (EX, False),
        "src/{Name}.Application/Orders/CancelEmptyOrdersCommand.cs": (EX, False),
        "src/{Name}.Infrastructure/Scheduling/SchedulerService.cs": ({}, False),
        "src/{Name}.Infrastructure/Scheduling/SchedulerModule.cs": ({}, False),
        "src/{Name}.Infrastructure/Scheduling/SchedulerSettings.cs": ({}, False),
        "src/{Name}.Infrastructure/Scheduling/SchedulerTicks.cs": ({}, False),
        "src/{Name}.Infrastructure/Scheduling/RecurringJobRegistry.cs": ({}, False),
        "src/{Name}.Infrastructure/Scheduling/ScheduledJobRun.cs": ({}, False),
        "src/{Name}.Infrastructure/Scheduling/ScheduledJobRunConfiguration.cs": ({}, False),
        "test/{Name}.Tests/Infrastructure/Scheduling/SchedulerTicksTests.cs": ({}, False),
        "test/{Name}.Tests/Application/Orders/CancelEmptyOrdersCommandHandlerTests.cs": (EX, False),
    }),
    "storage.md": ({"addons": "storage"}, "docs/adr/0027-file-storage-port.md", {
        "src/{Name}.Shared/Application/IFileStorage.cs": ({}, True),
        "src/{Name}.Shared/Application/StoredFile.cs": ({}, True),
        "src/{Name}.Infrastructure/Storage/S3FileStorage.cs": ({}, False),
        "src/{Name}.Infrastructure/Storage/LocalDiskFileStorage.cs": ({}, False),
        "src/{Name}.Infrastructure/Storage/StorageModule.cs": ({}, False),
        "src/{Name}.Infrastructure/Storage/StorageSettings.cs": ({}, False),
        "src/{Name}.Infrastructure/Storage/StorageConstants.cs": ({}, False),
        "src/{Name}.Domain/Aggregates/Products/Events/ImageAttached.cs": (EX, False),
        "src/{Name}.Application/Products/AttachProductImageCommand.cs": (EX, False),
        "src/{Name}.Application/Products/GetProductImageCommand.cs": (EX, False),
        "test/{Name}.Tests/Infrastructure/Storage/LocalDiskFileStorageTests.cs": ({}, False),
        "test/{Name}.Tests/TestSupport/Shared/Application/FileStorageBuilder.cs": ({}, False),
        "test/{Name}.Tests/Application/Products/AttachProductImageCommandHandlerTests.cs": (EX, False),
        "test/{Name}.IntegrationTests/Storage/S3FileStorageTests.cs": ({"testing": ["integration", "architecture"]}, False),
    }),
    "secrets.md": ({"addons": "secrets", "database": "postgres"}, "docs/adr/0028-secrets-at-rest.md", {
        "src/{Name}.Shared/Application/ISecretProtector.cs": ({}, True),
        "src/{Name}.Infrastructure/Secrets/EnvelopeSecretProtector.cs": ({}, False),
        "src/{Name}.Infrastructure/Secrets/SecretsModule.cs": ({}, False),
        "src/{Name}.Infrastructure/Secrets/SecretsSettings.cs": ({}, False),
        "src/{Name}.Infrastructure/Secrets/SecretsConstants.cs": ({}, False),
        "src/{Name}.Infrastructure/Secrets/SecretPropertyExtensions.cs": ({}, False),
        "src/{Name}.Domain/Aggregates/Customers/Events/TaxNumberSet.cs": (EX, False),
        "src/{Name}.Application/Customers/SetCustomerTaxNumberCommand.cs": (EX, False),
        "test/{Name}.Tests/Infrastructure/Secrets/EnvelopeSecretProtectorTests.cs": ({}, False),
        "src/{Name}.Worker/appsettings.Development.json": ({"hosts.worker": "separate"}, False),
        "test/{Name}.Tests/TestSupport/Infrastructure/Secrets/FakeSecretsSettings.cs": ({}, False),
        "test/{Name}.Tests/Application/Customers/SetCustomerTaxNumberCommandHandlerTests.cs": (EX, False),
        "test/{Name}.Tests/Infrastructure/Persistence/Customers/CustomerConfigurationTests.cs": (EX, False),
    }),
    "loop-guard.md": ({"addons": "loop-guard"}, "docs/adr/0029-loop-guard.md", {
        "src/{Name}.Shared/Application/LoopGuardConstants.cs": ({}, True),
        "src/{Name}.Shared/Exceptions/LoopDetectedException.cs": ({}, True),
        "test/{Name}.Tests/TestSupport/Shared/Application/PingPongLog.cs": ({}, False),
        "test/{Name}.Tests/Shared/Application/LoopGuardTests.cs": ({}, False),
    }),
    "contexts.md": ({"addons": "contexts"}, "docs/adr/0030-bounded-contexts.md", {
        "test/{Name}.Tests/Architecture/ContextRulesTests.cs": ({}, False),
        "test/{Name}.Tests/TestSupport/Architecture/ContextReferences.cs": ({}, False),
    }),
    "events-project.md": ({"addons": "events-project"}, "docs/adr/0031-event-contracts-project.md", {
        "src/{Name}.Shared/Domain/MessageNameAttribute.cs": ({}, True),
        "src/{Name}.Contracts/{Name}.Contracts.csproj": ({}, False),
        "src/{Name}.Contracts/ContractsMarker.cs": ({}, False),
        "src/{Name}.Contracts/Orders/LineAdded.cs": (EX, False),
        "src/{Name}.Contracts/Orders/LineCancelled.cs": (EX, False),
        "src/{Name}.Contracts/Orders/Completed.cs": (EX, False),
        "src/{Name}.Contracts/Orders/Cancelled.cs": (EX, False),
        "src/{Name}.Contracts/Products/StockIncreased.cs": (EX, False),
        "src/{Name}.Contracts/Products/StockDecreased.cs": (EX, False),
        "test/{Name}.Tests/TestSupport/Infrastructure/Messaging/PinnedProbe.cs": ({"events.transport": "rabbitmq"}, False),
        "test/{Name}.Tests/Architecture/ContractsRulesTests.cs": ({}, False),
    }),
}
MOVED_BY_EVENTS_PROJECT = {f"src/Boilerplate.Domain/Aggregates/{aggregate}/Events/{event}.cs" for aggregate, events in (("Orders", ("LineAdded", "LineCancelled", "Completed", "Cancelled")), ("Products", ("StockIncreased", "StockDecreased"))) for event in events}
OPTION_CHANGES = {
    "api-conventions": ["src/{Name}.Api/Program.cs", "src/{Name}.Api/Middlewares/ApiConstants.cs",
                        "src/{Name}.Domain/Aggregates/Customers/Customer.cs", "src/{Name}.Domain/Aggregates/Customers/CustomerConstants.cs", "src/{Name}.Application/Customers/CustomerDto.cs",
                        "src/{Name}.Application/Customers/CreateCustomerCommand.cs", "src/{Name}.Application/ApplicationModule.cs", "src/{Name}.Infrastructure/Persistence/Customers/CustomerConfiguration.cs",
                        "src/{Name}.Api/Controllers/CustomersController.cs"],
    "idempotent-commands": ["src/{Name}.Api/Program.cs", "src/{Name}.Api/appsettings.json", "src/{Name}.Infrastructure/Persistence/AppDbContext.cs"],
    "soft-delete": ["src/{Name}.Domain/Aggregates/Products/Product.cs", "src/{Name}.Domain/Aggregates/Products/IProductRepository.cs", "src/{Name}.Infrastructure/Persistence/Products/ProductRepository.cs", "src/{Name}.Infrastructure/Persistence/Products/ProductConfiguration.cs", "src/{Name}.Api/Controllers/ProductsController.cs", "src/{Name}.Application/ApplicationModule.cs", "src/{Name}.Infrastructure/Persistence/AppDbContext.cs", "src/{Name}.Infrastructure/Persistence/PersistenceConstants.cs"],
    "caching": ["src/{Name}.Application/Products/GetProductCommand.cs", "src/{Name}.Domain/Aggregates/Products/Product.cs", "src/{Name}.Application/ApplicationModule.cs", "src/{Name}.Infrastructure/InfrastructureModule.cs", "src/{Name}.Api/appsettings.json", "docker-compose.yml"],
    "scheduler": ["src/{Name}.Worker/Program.cs", "src/{Name}.Worker/appsettings.json", "src/{Name}.Domain/Aggregates/Orders/IOrderRepository.cs", "src/{Name}.Domain/Aggregates/Orders/OrderConstants.cs", "src/{Name}.Infrastructure/Persistence/Orders/OrderRepository.cs", "src/{Name}.Application/ApplicationModule.cs"],
    "storage": ["src/{Name}.Domain/Aggregates/Products/Product.cs", "src/{Name}.Application/Products/ProductDto.cs", "src/{Name}.Domain/Aggregates/Products/ProductConstants.cs", "src/{Name}.Api/Controllers/ProductsController.cs", "src/{Name}.Application/ApplicationModule.cs", "src/{Name}.Infrastructure/InfrastructureModule.cs", "src/{Name}.Api/appsettings.json", "docker-compose.yml"],
    "secrets": ["src/{Name}.Domain/Aggregates/Customers/Customer.cs", "src/{Name}.Application/Customers/CustomerDto.cs", "src/{Name}.Infrastructure/Persistence/Customers/CustomerConfiguration.cs", "src/{Name}.Api/Controllers/CustomersController.cs", "src/{Name}.Infrastructure/Persistence/AppDbContext.cs", "src/{Name}.Application/ApplicationModule.cs", "src/{Name}.Infrastructure/InfrastructureModule.cs", "src/{Name}.Console/Program.cs", "src/{Name}.Api/appsettings.json"],
    "loop-guard": ["src/{Name}.Shared/Application/CommandContext.cs", "src/{Name}.Shared/Domain/EventBase.cs", "src/{Name}.Shared/Domain/EventContext.cs", "src/{Name}.Shared/Application/CommandPipeline.cs", "src/{Name}.Infrastructure/Logging/LogFields.cs"],
    "contexts": ["every example file: a Contexts/{Context} segment in its path and namespace"],
    "events-project": ["{Name}.slnx", "src/{Name}.Domain/{Name}.Domain.csproj", "src/{Name}.Infrastructure/Messaging/RabbitMq/MessageNames.cs", "src/{Name}.Domain/Aggregates/Orders/Order.cs", "docs/specs/messaging-wire-format.md"],
    "testing": ["{Name}.slnx", "Directory.Packages.props", "test/{Name}.Tests/{Name}.Tests.csproj"],
    "observability": ["src/{Name}.Api/Program.cs", "src/{Name}.Worker/Program.cs", "src/{Name}.Api/appsettings.json", "src/{Name}.Worker/appsettings.json",
                      "src/{Name}.Shared/Application/CommandPipeline.cs", "src/{Name}.Shared/Domain/EventHandlerExecutor.cs", "src/{Name}.Infrastructure/Messaging/EventPublisher.cs",
                      "src/{Name}.Infrastructure/Messaging/Outbox/OutboxMessage.cs", "src/{Name}.Infrastructure/Messaging/Outbox/OutboxMessageConfiguration.cs", "src/{Name}.Infrastructure/Messaging/Outbox/OutboxSettings.cs",
                      "src/{Name}.Infrastructure/Messaging/Outbox/OutboxWorker.cs", "src/{Name}.Infrastructure/Messaging/RabbitMq/MessageEnvelope.cs", "src/{Name}.Infrastructure/Messaging/RabbitMq/ReceiveEndpointService.cs",
                      "src/{Name}.Infrastructure/Messaging/RabbitMq/RabbitMqConstants.cs", "src/{Name}.Infrastructure/Logging/JsonLineFormatter.cs", "src/{Name}.Infrastructure/Logging/LoggingConstants.cs"],
}
GET_COMMAND_FILES = [f"src/Boilerplate.Application/{folder}/Get{name}Command.cs" for folder, name in (("Products", "Product"), ("Orders", "Order"), ("Customers", "Customer"), ("Categories", "Category"))]
EVENT_CONSUMER_FILES = ("src/Boilerplate.Infrastructure/Messaging/EventConsumer", "test/Boilerplate.Tests/Infrastructure/Messaging/EventConsumer")
OUTBOX_PREFIXES = ("src/Boilerplate.Infrastructure/Messaging/Outbox/", "test/Boilerplate.Tests/Infrastructure/Messaging/Outbox/")
RABBITMQ_PREFIXES = ("src/Boilerplate.Infrastructure/Messaging/RabbitMq/", "test/Boilerplate.Tests/Infrastructure/Messaging/RabbitMq/")
OPTION_PAGES = {"src/Boilerplate.Infrastructure/Persistence/DatabaseExceptionHandler.cs": "infrastructure-postgres.md", "docs/adr/0009-correlation-header.md": "project-docs-adr.md",
                "docs/adr/0011-outbox-and-publishing-models.md": "project-docs-adr.md",
                "docs/adr/0013-own-bus-compatible-with-masstransit.md": "project-docs-adr.md",
                "docs/adr/0014-bearer-tokens-fill-the-context.md": "project-docs-adr.md",
                "docs/adr/0015-multi-tenancy-by-interface.md": "project-docs-adr.md",
                "docs/adr/0016-languages-of-messages-and-data.md": "project-docs-adr.md"}
KERNEL_PAGES = {"src/Boilerplate.Api/Middlewares/ErrorReference.cs": "failures.md"}
SKIPPED = ("MORTIS.md",)


def files_of(preset):
    root = VARIANTS / preset
    return {str(p.relative_to(root)) for p in root.rglob("*") if p.is_file() and not ({"bin", "obj", ".mortis", "Migrations"} & set(p.relative_to(root).parts)) and str(p.relative_to(root)) not in SKIPPED}


present = {preset: files_of(preset) for preset in PRESETS}
files = sorted(set().union(*present.values()))
deletes = [l.split(" ", 1)[1] for l in (TEMPLATE / "csharp-example.txt").read_text().splitlines() if l.startswith("delete ")]
deletes += ["src/Boilerplate.Application/Categories", "src/Boilerplate.Infrastructure/Persistence/Categories"]


def is_example(f):
    return any(f == d or f.startswith(d + "/") for d in deletes) or "/Categories/" in f


def page_of(f):
    if f in ("BannedSymbols.txt","Directory.Build.props","Directory.Packages.props","global.json",".editorconfig",".gitignore","Boilerplate.slnx","Dockerfile","docker-compose.yml") or f.endswith(".csproj"): return "solution.md"
    if f.startswith(".vscode/"): return "hosts.md"
    if f.startswith("docs/adr/"): return "project-docs-adr.md"
    if f == "docs/architecture.md": return "project-docs-architecture.md"
    if f.endswith(".md"): return "project-docs.md"
    if "/Migrations/" in f: return "infrastructure-postgres.md"
    if f.startswith("test/"):
        if is_example(f): return "worked-slice-tests.md"
        if "Infrastructure/Messaging/EventPublisher" in f: return "events-inprocess.md"
        if "/Console/" in f and "TestSupport" not in f: return "hosts.md"
        return "testing.md" if "/TestSupport/" in f else "testing-kernel.md"
    if "Boilerplate.Shared/Domain" in f: return "shared-domain.md"
    if "Boilerplate.Shared/Application" in f: return "shared-application.md"
    if "Boilerplate.Shared/" in f: return "shared-other.md"
    if "Boilerplate.Infrastructure/Messaging" in f: return "events-inprocess.md"
    if "Boilerplate.Infrastructure/Logging" in f or "Tests/Infrastructure/Logging" in f: return "logging.md"
    if "Boilerplate.Infrastructure" in f: return "infrastructure-postgres.md"
    if "Boilerplate.Api" in f or "Boilerplate.Console" in f: return "hosts.md"
    if "Boilerplate.Domain" in f: return "domain.md" if not is_example(f) else "worked-slice.md"
    if "Boilerplate.Application" in f: return "application.md" if not is_example(f) else "worked-slice.md"
    return "solution.md"
def source_of(f):
    if f == "src/Boilerplate.Worker/Program.cs":
        return "data"
    return "snippets" if f.endswith(".cs") else "data"


pending = []
pending.append({"path": "src/{Name}.Shared/Domain/TenancyConstants.cs", "page": "tenancy.md", "source": "snippets", "when": {"tenancy.mode": "interface", "tenancy.key": "text"}, "kernel": True})
for path, (when, kernel) in MONGO_FILES.items():
    pending.append({"path": path, "page": "infrastructure-mongo.md", "source": "snippets", "when": {**when, "database": "mongo"}, "kernel": kernel})
for path, (when, kernel) in QUERY_FILES.items():
    if path.endswith(("/PageRequest.cs", "/PagedResult.cs")):
        pending.append({"path": path, "page": "queries.md", "source": "snippets", "when_any": [{"queries": ["split", "read-models"]}, {"addons": "api-conventions"}], "kernel": kernel, "unbuilt": True})
        continue
    pending.append({"path": path, "page": "queries.md", "source": "snippets", "when": {**when, "queries": ["split", "read-models"]}, "kernel": kernel, "unbuilt": True})
for path, (when, kernel) in READ_MODEL_FILES.items():
    pending.append({"path": path, "page": "read-models.md", "source": "snippets", "when": {**when, "queries": "read-models", "database": "postgres"}, "kernel": kernel, "unbuilt": True})
for path, (when, kernel) in API_CONVENTION_FILES.items():
    pending.append({"path": path, "page": "api-conventions.md", "source": "snippets", "when": {**when, "addons": "api-conventions"}, "kernel": kernel, "unbuilt": True})
for path, when in IDEMPOTENCY_FILES.items():
    pending.append({"path": path, "page": "idempotency.md", "source": "snippets", "when": {**when, "addons": "idempotent-commands", "database": "postgres"}, "kernel": False, "unbuilt": True})
for path, (when, kernel) in OBSERVABILITY_FILES.items():
    pending.append({"path": path, "page": "observability.md", "source": "snippets", "when": {**when, "addons": "observability"}, "kernel": kernel, "unbuilt": True})
pending.append({"path": "docs/adr/0020-one-shape-for-requests-and-failures.md", "page": "project-docs-adr.md", "source": "snippets", "when": {"addons": "api-conventions"}, "kernel": False, "unbuilt": True})
pending.append({"path": "docs/adr/0021-idempotency-keys-for-post.md", "page": "project-docs-adr.md", "source": "snippets", "when": {"addons": "idempotent-commands", "database": "postgres"}, "kernel": False, "unbuilt": True})
pending.append({"path": "docs/adr/0022-health-traces-and-metrics.md", "page": "project-docs-adr.md", "source": "snippets", "when": {"addons": "observability"}, "kernel": False, "unbuilt": True})
for path, when in TESTING_FILES.items():
    pending.append({"path": path, "page": "testing-levels.md", "source": "snippets", "when": when, "kernel": False, "unbuilt": True})
pending.append({"path": "docs/adr/0023-testing-levels.md", "page": "project-docs-adr.md", "source": "snippets", "when": {"testing": HANDLERS_UP}, "kernel": False, "unbuilt": True})
for page, (base, adr, addon_files) in ADVANCED_ADDONS.items():
    for path, (when, kernel) in addon_files.items():
        pending.append({"path": path, "page": page, "source": "snippets", "when": {**base, **when}, "kernel": kernel, "unbuilt": True})
    pending.append({"path": adr, "page": "project-docs-adr.md", "source": "snippets", "when": base, "kernel": False, "unbuilt": True})
for path, when in PERMISSION_FILES.items():
    pending.append({"path": path, "page": "permissions.md", "source": "snippets", "when": {**when, "auth": "jwt"}, "kernel": False, "unbuilt": True})
pending.append({"path": "docs/adr/0019-every-command-declares-a-permission.md", "page": "project-docs-adr.md", "source": "snippets", "when": {"auth": "jwt"}, "kernel": False, "unbuilt": True})
pending.append({"path": "docs/adr/0017-aggregates-as-documents-in-mongodb.md", "page": "project-docs-adr.md", "source": "snippets", "when": {"database": "mongo"}, "kernel": False})
pending.append({"path": "docs/adr/0018-queries-and-read-models.md", "page": "project-docs-adr.md", "source": "snippets", "when": {"queries": ["split", "read-models"]}, "kernel": False, "unbuilt": True})
PENDING = {entry["path"]: entry for entry in pending}
used = set()
out = []
for f in files:
    entry = {"path": f.replace("Boilerplate", "{Name}"), "page": KERNEL_PAGES.get(f, page_of(f)), "source": source_of(f)}
    ex = is_example(f)
    if ex:
        entry["when"] = {"example": "true"}
    entry["kernel"] = (not ex) and f.endswith(".cs") and not f.startswith("test/")
    if f.startswith(OUTBOX_PREFIXES):
        entry["when"] = {"events.outbox": OUTBOX}
        entry["kernel"] = False
        entry["page"] = "outbox.md"
    if f.startswith(RABBITMQ_PREFIXES):
        entry["when"] = {"events.transport": "rabbitmq"}
        entry["kernel"] = False
        entry["page"] = "rabbitmq.md"
    if f.startswith(AUTH_PREFIXES):
        entry["when"] = {"auth": "jwt"}
        entry["kernel"] = False
        entry["page"] = "authentication.md"
    if f.startswith(TENANCY_PREFIXES):
        entry["when"] = {"tenancy.mode": "interface"}
        entry["kernel"] = False
        entry["page"] = "tenancy.md"
    if f.startswith(MESSAGES_PREFIXES):
        entry["when"] = {"languages.mode": ["messages", "data"]}
        entry["kernel"] = False
        entry["page"] = "languages.md"
    if f.startswith(DATA_PREFIXES):
        entry["when"] = {"languages.mode": "data"}
        entry["kernel"] = False
        entry["page"] = "languages.md"
    if f in DATA_EXAMPLE_FILES:
        entry["when"] = {"example": "true", "languages.mode": "data"}
        entry["page"] = "languages.md"
    if f.startswith(EVENT_CONSUMER_FILES):
        entry["kernel"] = False
        entry["page"] = "event-consumers.md"
    if f in OPTION_FILES:
        entry["when"] = OPTION_FILES[f]
        entry["kernel"] = False
        entry["page"] = "event-consumers.md" if f.startswith(EVENT_CONSUMER_FILES) else OPTION_PAGES.get(f, "correlation.md" if "Correlation" in f else "outbox.md" if "Worker" in f else "events-inprocess.md" if ("EventPublisher" in f or "MessagingConstants" in f) else "failures.md")
    if f in GET_COMMAND_FILES:
        entry["when"] = {**entry.get("when", {}), "queries": "commands"}
    if f.startswith(POSTGRES_PREFIXES) and f not in POSTGRES_EXCEPTIONS:
        entry["when"] = {**entry.get("when", {}), "database": "postgres"}
    if f == "test/Boilerplate.Tests/Infrastructure/Messaging/EventConsumerModuleTests.cs":
        entry["when"] = {"example": "true"}
    if f in MOVED_BY_EVENTS_PROJECT:
        entry["when_not"] = {"addons": "events-project"}
    known = PENDING.get(entry["path"])
    if known is not None:
        used.add(entry["path"])
        entry["page"] = known["page"]
        entry["kernel"] = known["kernel"]
        entry.pop("when", None)
        for key in ("when", "when_any"):
            if key in known:
                entry[key] = known[key]
    in_all = all(f in present[preset] for preset in PRESETS)
    assert in_all or "when_any" in entry or "when_not" in entry or "when" in entry and set(entry["when"]) != {"example"}, f"{f} is in only some variants and has no option"
    out.append(entry)
out += [entry for entry in pending if entry["path"] not in used]
initial_when = [{"example": "true", "database": "postgres"}, {"events.outbox": OUTBOX, "database": "postgres"}]
out.append({"path": "src/{Name}.Infrastructure/Persistence/Migrations/*_Initial.cs", "page": "infrastructure-postgres.md", "source": "tool", "when_any": initial_when, "kernel": False, "glob": True})
out.append({"path": "src/{Name}.Infrastructure/Persistence/Migrations/*_Initial.Designer.cs", "page": "infrastructure-postgres.md", "source": "tool", "when_any": initial_when, "kernel": False, "glob": True})
out.append({"path": "src/{Name}.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs", "page": "infrastructure-postgres.md", "source": "tool", "when_any": initial_when, "kernel": False})
out.append({"path": "MORTIS.md", "page": "project-docs.md", "source": "tool", "kernel": False})
out.sort(key=lambda e: e["path"])
manifest={
 "language":"csharp",
 "manifest_version":1,
 "placeholders":{"Name":"the `name` row of MORTIS.md","Aggregate":"an aggregate root's type name, singular PascalCase","Plural":"the aggregate's folder name, plural PascalCase"},
 "files":out,
 "aggregate_files":{
   "description":"Files a project adds per aggregate. Every file in these folders must match one pattern; verify.sh fails on any other file there.",
   "folders":{
     "src/{Name}.Domain/Aggregates/{Plural}":["{Aggregate}.cs","I{Aggregate}Repository.cs","{Aggregate}Constants.cs","{Aggregate}Status.cs","*Model.cs","Events/*.cs","*.cs"],
     "src/{Name}.Application/{Plural}":["{Aggregate}Dto.cs","*Command.cs","*When*EventHandler.cs"],
     "src/{Name}.Infrastructure/Persistence/{Plural}":["{Aggregate}Configuration.cs","{Aggregate}Repository.cs"],
     "src/{Name}.Infrastructure/Persistence/Mongo/{Plural}":["{Aggregate}Repository.cs"],
     "src/{Name}.Api/Controllers":["{Plural}Controller.cs"],
     "test/{Name}.Tests/Domain/{Plural}":["*Tests.cs"],
     "test/{Name}.Tests/Application/{Plural}":["*CommandHandlerTests.cs","*EventHandlerTests.cs"],
     "test/{Name}.Tests/Infrastructure/Persistence/{Plural}":["{Aggregate}ConfigurationTests.cs","{Aggregate}RepositoryTests.cs"],
     "test/{Name}.Tests/TestSupport/Domain/{Plural}":["Fake*.cs","{Aggregate}RepositoryBuilder.cs"]
   }
 }
}
old = json.load(open(TARGET))
manifest["aggregate_files"] = old["aggregate_files"]
manifest["aggregate_files"]["folders"].setdefault("src/{Name}.Infrastructure/Persistence/Mongo/{Plural}", ["{Aggregate}Repository.cs"])
manifest["aggregate_files"]["folders"]["src/{Name}.Infrastructure/Persistence/Mongo/{Plural}"] = ["{Aggregate}Repository.cs", "{Aggregate}Reader.cs"]
manifest["aggregate_files"]["folders"]["src/{Name}.Contracts/{Plural}"] = ["*.cs"]
for folder, patterns in (("src/{Name}.Domain/Aggregates/{Plural}", ["I{Aggregate}Reader.cs", "*View.cs"]), ("src/{Name}.Application/{Plural}", ["*Query.cs", "{Aggregate}Permissions.cs"]), ("src/{Name}.Infrastructure/Persistence/{Plural}", ["{Aggregate}Reader.cs"]), ("test/{Name}.Tests/Application/{Plural}", ["*QueryHandlerTests.cs", "*ValidatorTests.cs"]), ("test/{Name}.Tests/Infrastructure/Persistence/{Plural}", ["{Aggregate}ReaderTests.cs"]), ("test/{Name}.Tests/TestSupport/Domain/{Plural}", ["{Aggregate}ReaderBuilder.cs"]), ("test/{Name}.IntegrationTests/Persistence", ["{Aggregate}RepositoryTests.cs"])):
    existing = manifest["aggregate_files"]["folders"].setdefault(folder, [])
    for pattern in patterns:
        if pattern not in existing:
            existing.insert(len(existing) - 1 if existing and existing[-1] == "*.cs" else len(existing), pattern)
manifest["always_allowed"] = old["always_allowed"]
manifest["option_changes"] = {"description": "Existing files an option not built yet changes; the option's page says how.", "options": OPTION_CHANGES}
json.dump(manifest, open(TARGET, "w"), indent=1)
print(len(out), "entries")
