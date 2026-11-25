using Boilerplate.Shared.Application;
using Boilerplate.Shared.Exceptions;
using System.Reflection;
using System.Text.Json;

namespace Boilerplate.Console;

public class ConsoleCommandRunner(ICommandDispatcher commandDispatcher, Assembly commandAssembly, IQueryDispatcher queryDispatcher = null)
{
    public async Task<object> RunAsync(string name, string argument, CommandContext commandContext)
    {
        var commandType = commandAssembly.GetTypes()
            .FirstOrDefault(type => !type.IsAbstract && type.Name.Equals(name + ConsoleConstants.CommandSuffix, StringComparison.OrdinalIgnoreCase));

        var queryType = commandType is null && queryDispatcher is not null
            ? commandAssembly.GetTypes().FirstOrDefault(type => !type.IsAbstract && typeof(IQuery).IsAssignableFrom(type) && type.Name.Equals(name + ConsoleConstants.QuerySuffix, StringComparison.OrdinalIgnoreCase))
            : null;

        if (queryType is not null)
        {
            var query = (IQuery)await DeserializeAsync(queryType, argument);
            query.Context = commandContext;

            var resultType = queryType.GetInterfaces().First(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IQuery<>)).GetGenericArguments()[0];
            var ask = typeof(IQueryDispatcher).GetMethod(nameof(IQueryDispatcher.Ask)).MakeGenericMethod(resultType);

            return await (dynamic)ask.Invoke(queryDispatcher, [query]);
        }

        var contentType = commandType?.GetInterfaces()
            .FirstOrDefault(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(ICommand<>))?
            .GetGenericArguments()[0]
            ?? throw new InvalidParameterException("command", $"{name} is not a command");

        var command = await DeserializeAsync(commandType, argument);

        if (command is IContextCommand contextCommand)
        {
            contextCommand.Context = commandContext;
        }

        var send = typeof(ICommandDispatcher).GetMethod(nameof(ICommandDispatcher.Send)).MakeGenericMethod(contentType);
        dynamic commandResult = await (dynamic)send.Invoke(commandDispatcher, [command]);

        return commandResult.Content;
    }

    private static async Task<object> DeserializeAsync(Type type, string argument)
    {
        var json = string.IsNullOrWhiteSpace(argument) ? "{}"
            : argument.StartsWith(ConsoleConstants.FilePrefix) ? await File.ReadAllTextAsync(argument[ConsoleConstants.FilePrefix.Length..])
            : argument;

        object value;

        try
        {
            value = JsonSerializer.Deserialize(json, type, ConsoleConstants.InputOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidParameterException("arguments", exception.Message, exception);
        }

        return value ?? throw new InvalidParameterException("arguments", "a JSON object is required");
    }
}
