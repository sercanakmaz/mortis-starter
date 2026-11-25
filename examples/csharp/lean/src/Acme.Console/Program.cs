using Microsoft.Extensions.DependencyInjection;
using Acme.Infrastructure.Logging;
using Microsoft.Extensions.Hosting;
using Acme.Shared.Application;
using Acme.Shared.Exceptions;
using Acme.Infrastructure.Messaging;
using Acme.Application.Products;
using Acme.Infrastructure;
using Acme.Application;
using Acme.Console;
using Acme.Domain;
using Acme.Shared;
using System.Text.Json;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { Args = args, ContentRootPath = AppContext.BaseDirectory });

builder.Logging.AddJsonLogging(toStandardError: true);

builder.Services
    .AddSharedModule()
    .AddDomainModule()
    .AddApplicationModule()
    .AddInfrastructureModule(builder.Configuration)
    .AddEventConsumer<DecreaseStockWhenOrderLineAddedEventConsumer>(endpoint => endpoint.Retry(3))
    .AddEventConsumer<IncreaseStockWhenOrderLineCancelledEventConsumer>(endpoint => endpoint.Retry(3));

using var host = builder.Build();

if (args.Length == 0)
{
    System.Console.Error.WriteLine("Usage: dotnet run -- --user <id> [--email <email>] <Command> [json | @file.json]");

    return 1;
}

using var scope = host.Services.CreateScope();

var consoleCommandRunner = new ConsoleCommandRunner(scope.ServiceProvider.GetRequiredService<ICommandDispatcher>(), typeof(ApplicationModule).Assembly);

try
{
    var consoleArguments = ConsoleArguments.Parse(args);
    var content = await consoleCommandRunner.RunAsync(consoleArguments.Name, consoleArguments.Argument, consoleArguments.CommandContext);

    System.Console.WriteLine(JsonSerializer.Serialize(content, ConsoleConstants.OutputOptions));

    return 0;
}
catch (ExceptionBase exception)
{
    var errors = (exception as ValidationFailedException)?.Errors;

    System.Console.Error.WriteLine(JsonSerializer.Serialize(new { code = exception.Code, message = exception.Message, errors }));

    return 2;
}
