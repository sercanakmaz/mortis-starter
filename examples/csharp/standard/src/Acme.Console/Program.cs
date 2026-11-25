using System.Text.Json;
using Acme.Application;
using Acme.Application.Products;
using Acme.Console;
using Acme.Domain;
using Acme.Infrastructure;
using Acme.Infrastructure.Authentication;
using Acme.Infrastructure.Logging;
using Acme.Infrastructure.Messaging;
using Acme.Shared;
using Acme.Shared.Application;
using Acme.Shared.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

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
    System.Console.Error.WriteLine("Usage: dotnet run -- --user <id> [--email <email>] [--language <tag>] <Command> [json | @file.json]   or   dotnet run -- dev-token [json]");

    return 1;
}

using var scope = host.Services.CreateScope();

var consoleCommandRunner = new ConsoleCommandRunner(scope.ServiceProvider.GetRequiredService<ICommandDispatcher>(), typeof(ApplicationModule).Assembly, scope.ServiceProvider.GetRequiredService<IQueryDispatcher>());

try
{
    if (args[0] == ConsoleConstants.DevelopmentTokenCommand)
    {
        var consoleTokens = new ConsoleTokens(host.Services.GetRequiredService<IOptions<AuthSettings>>().Value, host.Services.GetRequiredService<IHostEnvironment>());

        System.Console.WriteLine(consoleTokens.WriteDevelopmentToken(args.ElementAtOrDefault(1)));

        return 0;
    }

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
