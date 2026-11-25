using System.Reflection;
using System.Text.Json;
using Acme.Shared.Application;
using Acme.Shared.Exceptions;

namespace Acme.Console;

public class ConsoleCommandRunner(ICommandDispatcher commandDispatcher, Assembly commandAssembly)
{
    public async Task<object> RunAsync(string name, string argument, CommandContext commandContext)
    {
        var commandType = commandAssembly.GetTypes()
            .FirstOrDefault(type => !type.IsAbstract && type.Name.Equals(name + ConsoleConstants.CommandSuffix, StringComparison.OrdinalIgnoreCase));

        var contentType = commandType?.GetInterfaces()
            .FirstOrDefault(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(ICommand<>))?
            .GetGenericArguments()[0]
            ?? throw new InvalidParameterException("command", $"{name} is not a command");

        var json = string.IsNullOrWhiteSpace(argument) ? "{}"
            : argument.StartsWith(ConsoleConstants.FilePrefix) ? await File.ReadAllTextAsync(argument[ConsoleConstants.FilePrefix.Length..])
            : argument;

        object command;

        try
        {
            command = JsonSerializer.Deserialize(json, commandType, ConsoleConstants.InputOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidParameterException("arguments", exception.Message, exception);
        }

        if (command is null)
        {
            throw new InvalidParameterException("arguments", "a JSON object is required");
        }

        if (command is IContextCommand contextCommand)
        {
            contextCommand.Context = commandContext;
        }

        var send = typeof(ICommandDispatcher).GetMethod(nameof(ICommandDispatcher.Send)).MakeGenericMethod(contentType);
        dynamic commandResult = await (dynamic)send.Invoke(commandDispatcher, [command]);

        return commandResult.Content;
    }
}
