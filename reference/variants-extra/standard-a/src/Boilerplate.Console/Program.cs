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
using Boilerplate.Infrastructure.Persistence.Mongo;
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
    .AddEventConsumer<DecreaseStockWhenOrderLineAddedEventConsumer>(endpoint => endpoint.Retry(10))
    .AddEventConsumer<IncreaseStockWhenOrderLineCancelledEventConsumer>(endpoint => endpoint.Retry(10));

using var host = builder.Build();

if (args.Length == 0)
{
    System.Console.Error.WriteLine("Usage: dotnet run -- --user <id> [--email <email>] <Command> [json | @file.json]   or   dotnet run -- dev-token [json]");

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

    if (args[0] == ConsoleConstants.EnsureIndexesCommand)
    {
        await host.Services.GetRequiredService<MongoIndexes>().EnsureAsync();

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
