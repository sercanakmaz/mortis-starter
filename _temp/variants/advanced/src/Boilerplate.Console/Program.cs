using Microsoft.Extensions.DependencyInjection;
using Boilerplate.Infrastructure.Authentication;
using Boilerplate.Infrastructure.Tenancy;
using Boilerplate.Infrastructure.Localization;
using Microsoft.Extensions.Options;
using Boilerplate.Infrastructure.Logging;
using Microsoft.Extensions.Hosting;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Exceptions;
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
    .AddInfrastructureModule(builder.Configuration);

using var host = builder.Build();

if (args.Length == 0)
{
    System.Console.Error.WriteLine("Usage: dotnet run -- --user <id> [--email <email>] [--tenant <id>] [--language <tag>] <Command> [json | @file.json]   or   dotnet run -- dev-token [json]");

    return 1;
}

using var scope = host.Services.CreateScope();

var messageLocalizer = host.Services.GetRequiredService<MessageLocalizer>();
var language = Languages.Default;
var consoleCommandRunner = new ConsoleCommandRunner(scope.ServiceProvider.GetRequiredService<ICommandDispatcher>(), typeof(ApplicationModule).Assembly);

try
{
    if (args[0] == ConsoleConstants.DevelopmentTokenCommand)
    {
        var consoleTokens = new ConsoleTokens(host.Services.GetRequiredService<IOptions<AuthSettings>>().Value, host.Services.GetRequiredService<IHostEnvironment>());

        System.Console.WriteLine(consoleTokens.WriteDevelopmentToken(args.ElementAtOrDefault(1)));

        return 0;
    }

    var consoleArguments = ConsoleArguments.Parse(args);
    var commandContext = consoleArguments.CommandContext with { Language = Languages.Resolve([consoleArguments.CommandContext.Language]) };
    language = commandContext.Language;

    scope.ServiceProvider.GetRequiredService<TenantScope>().Enter(commandContext.TenantId);

    var content = await consoleCommandRunner.RunAsync(consoleArguments.Name, consoleArguments.Argument, commandContext);

    System.Console.WriteLine(JsonSerializer.Serialize(content, ConsoleConstants.OutputOptions));

    return 0;
}
catch (ExceptionBase exception)
{
    var errors = (exception as ValidationFailedException)?.Errors;

    System.Console.Error.WriteLine(JsonSerializer.Serialize(new { code = exception.Code, message = messageLocalizer.Get(exception.Code, exception.Parameters, language) ?? exception.Message, errors }));

    return 2;
}
