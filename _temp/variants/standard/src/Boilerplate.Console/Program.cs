using Microsoft.Extensions.DependencyInjection;
using Boilerplate.Infrastructure.Authentication;
using Microsoft.Extensions.Options;
using Boilerplate.Infrastructure.Logging;
using Microsoft.Extensions.Hosting;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Exceptions;
using Boilerplate.Infrastructure.Messaging;
using Boilerplate.Application.Products;
using Boilerplate.Infrastructure;
using Boilerplate.Application;
using Boilerplate.Console;
using Boilerplate.Domain;
using Boilerplate.Shared;
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

var arguments = args.ToList();
var token = Environment.GetEnvironmentVariable(ConsoleConstants.TokenVariable);

if (arguments.Count >= 2 && arguments[0] == ConsoleConstants.TokenOption)
{
    token = arguments[1];
    arguments.RemoveRange(0, 2);
}

if (arguments.Count == 0)
{
    System.Console.Error.WriteLine("Usage: dotnet run -- [--token <jwt>] <Command> [json | @file.json]   or   dotnet run -- dev-token [json]");

    return 1;
}

using var scope = host.Services.CreateScope();

var consoleTokens = new ConsoleTokens(host.Services.GetRequiredService<IOptions<AuthSettings>>().Value, host.Services.GetRequiredService<IHostEnvironment>());
var consoleCommandRunner = new ConsoleCommandRunner(scope.ServiceProvider.GetRequiredService<ICommandDispatcher>(), typeof(ApplicationModule).Assembly);

try
{
    if (arguments[0] == ConsoleConstants.DevelopmentTokenCommand)
    {
        System.Console.WriteLine(consoleTokens.WriteDevelopmentToken(arguments.ElementAtOrDefault(1)));

        return 0;
    }

    var commandContext = await consoleTokens.ContextOfAsync(token);

    var content = await consoleCommandRunner.RunAsync(arguments[0], arguments.ElementAtOrDefault(1), commandContext);

    System.Console.WriteLine(JsonSerializer.Serialize(content, ConsoleConstants.OutputOptions));

    return 0;
}
catch (ExceptionBase exception)
{
    var errors = (exception as ValidationFailedException)?.Errors;

    System.Console.Error.WriteLine(JsonSerializer.Serialize(new { code = exception.Code, message = exception.Message, errors }));

    return 2;
}
