using Boilerplate.Api.Middlewares;
using Boilerplate.Api.Filters;
using Boilerplate.Infrastructure.Logging;
using Boilerplate.Infrastructure.Messaging;
using Boilerplate.Application.Products;
using Boilerplate.Infrastructure;
using Boilerplate.Application;
using Boilerplate.Domain;
using Boilerplate.Shared;

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
    .AddEventConsumer<DecreaseStockWhenOrderLineAddedEventConsumer>(endpoint => endpoint.Retry(10))
    .AddEventConsumer<IncreaseStockWhenOrderLineCancelledEventConsumer>(endpoint => endpoint.Retry(10));

var app = builder.Build();

app.UseExceptionHandler();

app.MapOpenApi();
app.MapControllers();
app.MapGet("/health", () => Results.Ok());

app.Run();
