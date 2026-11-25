using Boilerplate.Infrastructure.Diagnostics;
using Boilerplate.Application.Orders;
using Boilerplate.Infrastructure.Messaging.Outbox;
using Boilerplate.Infrastructure.Scheduling;
using Boilerplate.Infrastructure.Messaging;
using Boilerplate.Application.Products;
using Boilerplate.Infrastructure.Logging;
using Microsoft.Extensions.Hosting;
using Boilerplate.Infrastructure;
using Boilerplate.Application;
using Boilerplate.Domain;
using Boilerplate.Shared;

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddJsonLogging();

builder.Services
    .AddSharedModule()
    .AddDomainModule()
    .AddApplicationModule()
    .AddInfrastructureModule(builder.Configuration)
    .AddObservability(builder.Configuration)
    .AddScheduler(builder.Configuration)
    .AddRecurringJob<CancelEmptyOrdersJob>()
    .AddOutboxWorker()
    .AddEventConsumer<DecreaseStockWhenOrderLineAddedEventConsumer>(endpoint => endpoint.Retry(10))
    .AddEventConsumer<IncreaseStockWhenOrderLineCancelledEventConsumer>(endpoint => endpoint.Retry(10));

using var host = builder.Build();

await host.RunAsync();
