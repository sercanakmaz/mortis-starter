using Boilerplate.Api.Authentication;
using Boilerplate.Api.Conventions;
using Boilerplate.Api.Middlewares;
using Microsoft.AspNetCore.Mvc.Authorization;
using Boilerplate.Api.Filters;
using Boilerplate.Infrastructure.Logging;
using Boilerplate.Infrastructure.Messaging.Outbox;
using Boilerplate.Infrastructure.Messaging;
using Boilerplate.Application.Products;
using Boilerplate.Infrastructure;
using Boilerplate.Application;
using Boilerplate.Domain;
using Boilerplate.Shared;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddJsonLogging();

builder.Services.AddControllers(options =>
{
    options.Filters.Add(new AuthorizeFilter());
    options.Filters.Add<ContextCommandFilter>();
}).AddApiConventions();
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ExceptionHandler>();

builder.Services
    .AddSharedModule()
    .AddDomainModule()
    .AddApplicationModule()
    .AddInfrastructureModule(builder.Configuration)
    .AddEventConsumer<DecreaseStockWhenOrderLineAddedEventConsumer>(endpoint => endpoint.Retry(10))
    .AddEventConsumer<IncreaseStockWhenOrderLineCancelledEventConsumer>(endpoint => endpoint.Retry(10))
    .AddOutboxWorker();

var app = builder.Build();

app.UseExceptionHandler();
app.UseApiConventions();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapControllers();
app.MapGet("/health", () => Results.Ok());

app.Run();
