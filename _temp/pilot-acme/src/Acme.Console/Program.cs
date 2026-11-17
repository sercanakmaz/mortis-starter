using Microsoft.Extensions.DependencyInjection;
using Acme.Console.Scenarios;
using Microsoft.Extensions.Hosting;
using Acme.Shared.Exceptions;
using System.Text.Json;
using Acme.Infrastructure;
using Acme.Application;
using Acme.Domain;
using Acme.Shared;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = args, ContentRootPath = AppContext.BaseDirectory });

builder.Services
    .AddSharedModule()
    .AddDomainModule()
    .AddApplicationModule()
    .AddInfrastructureModule(builder.Configuration)
    .AddScenariosModule();

using var host = builder.Build();
using var scope = host.Services.CreateScope();

var scenarios = scope.ServiceProvider.GetServices<IScenario>().ToList();
var scenario = scenarios.FirstOrDefault(candidate => string.Equals(candidate.Name, args.FirstOrDefault(), StringComparison.OrdinalIgnoreCase));

if (scenario is null)
{
    System.Console.WriteLine("Usage: dotnet run -- <scenario | command> [json | @file.json]");
    System.Console.WriteLine($"Names: {string.Join(", ", scenarios.Select(candidate => candidate.Name).Order())}");

    return 1;
}

try
{
    await scenario.RunAsync(args.Skip(1).ToArray());
}
catch (ExceptionBase exception)
{
    var errors = (exception as ValidationFailedException)?.Errors;

    System.Console.Error.WriteLine(JsonSerializer.Serialize(new { code = exception.Code, message = exception.Message, errors }, ScenarioConstants.OutputOptions));

    return 2;
}

return 0;
