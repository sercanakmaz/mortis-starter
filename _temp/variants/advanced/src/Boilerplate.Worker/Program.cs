using Boilerplate.Infrastructure.Messaging.Outbox;
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
    .AddOutboxWorker()
    .AddEventConsumer<DecreaseStockWhenOrderLineAddedEventConsumer>(endpoint => endpoint.Retry(3))
    .AddEventConsumer<IncreaseStockWhenOrderLineCancelledEventConsumer>(endpoint => endpoint.Retry(3));

using var host = builder.Build();

await host.RunAsync();
