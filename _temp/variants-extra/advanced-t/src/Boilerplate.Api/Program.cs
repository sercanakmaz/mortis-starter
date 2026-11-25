using Boilerplate.Api.Authentication;
using Boilerplate.Api.Conventions;
using Boilerplate.Api.Idempotency;
using Boilerplate.Api.Middlewares;
using Boilerplate.Infrastructure.Persistence.Idempotency;
using Boilerplate.Api.Observability;
using Boilerplate.Infrastructure.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using OpenTelemetry.Trace;
using Microsoft.AspNetCore.Mvc.Authorization;
using Boilerplate.Api.Filters;
using Boilerplate.Infrastructure.Logging;
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
    options.Filters.Add<IdempotencyFilter>();
}).AddApiConventions();
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ExceptionHandler>();
builder.Services.AddIdempotency(builder.Configuration);
builder.Services.AddObservability(builder.Configuration, tracing => tracing.AddAspNetCoreInstrumentation());

builder.Services
    .AddSharedModule()
    .AddDomainModule()
    .AddApplicationModule()
    .AddInfrastructureModule(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<RequestLoggingMiddleware>();
app.UseExceptionHandler();
app.UseApiConventions();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapControllers();
app.MapGet("/health", () => Results.Ok());
app.MapHealthChecks(ObservabilityConstants.LivePath, new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks(ObservabilityConstants.ReadyPath, new HealthCheckOptions { Predicate = check => check.Tags.Contains(ObservabilityConstants.ReadyTag), ResponseWriter = HealthResponse.WriteAsync });

app.Run();
