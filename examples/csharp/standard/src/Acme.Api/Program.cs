using Acme.Api.Authentication;
using Acme.Api.Conventions;
using Acme.Api.Filters;
using Acme.Api.Middlewares;
using Acme.Application;
using Acme.Application.Products;
using Acme.Domain;
using Acme.Infrastructure;
using Acme.Infrastructure.Logging;
using Acme.Infrastructure.Messaging;
using Acme.Infrastructure.Messaging.Outbox;
using Acme.Shared;
using Microsoft.AspNetCore.Mvc.Authorization;

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
    .AddOutboxWorker()
    .AddEventConsumer<DecreaseStockWhenOrderLineAddedEventConsumer>(endpoint => endpoint.Retry(3))
    .AddEventConsumer<IncreaseStockWhenOrderLineCancelledEventConsumer>(endpoint => endpoint.Retry(3));

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
