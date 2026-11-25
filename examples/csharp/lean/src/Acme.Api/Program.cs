using Acme.Api.Middlewares;
using Acme.Api.Filters;
using Acme.Infrastructure.Logging;
using Acme.Infrastructure.Messaging;
using Acme.Application.Products;
using Acme.Infrastructure;
using Acme.Application;
using Acme.Domain;
using Acme.Shared;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddJsonLogging();

builder.Services.AddControllers(options => options.Filters.Add<ContextCommandFilter>());
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ExceptionHandler>();

builder.Services
    .AddSharedModule()
    .AddDomainModule()
    .AddApplicationModule()
    .AddInfrastructureModule(builder.Configuration)
    .AddEventConsumer<DecreaseStockWhenOrderLineAddedEventConsumer>(endpoint => endpoint.Retry(3))
    .AddEventConsumer<IncreaseStockWhenOrderLineCancelledEventConsumer>(endpoint => endpoint.Retry(3));

var app = builder.Build();

app.UseExceptionHandler();

app.MapOpenApi();
app.MapControllers();
app.MapGet("/health", () => Results.Ok());

app.Run();
