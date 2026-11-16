using Boilerplate.Shared.Application;
using Boilerplate.Shared.Exceptions;
using System.Text.Json;

namespace Boilerplate.Console.Scenarios;

public class CommandScenario<TCommand, TContent>(ICommandDispatcher commandDispatcher) : IScenario
    where TCommand : ICommand<TContent>
{
    public string Name => typeof(TCommand).Name[..^ScenarioConstants.CommandSuffix.Length];

    public async Task RunAsync(string[] args)
    {
        var argument = args.FirstOrDefault();

        var json = string.IsNullOrWhiteSpace(argument) ? "{}"
            : argument.StartsWith(ScenarioConstants.FilePrefix) ? await File.ReadAllTextAsync(argument[ScenarioConstants.FilePrefix.Length..])
            : argument;

        TCommand command;

        try
        {
            command = JsonSerializer.Deserialize<TCommand>(json, ScenarioConstants.InputOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidParameterException("arguments", exception.Message, exception);
        }

        if (command is null)
        {
            throw new InvalidParameterException("arguments", "a JSON object is required");
        }

        var commandResult = await commandDispatcher.Send(command.AsConsoleUser());

        System.Console.WriteLine(JsonSerializer.Serialize(commandResult.Content, ScenarioConstants.OutputOptions));
    }
}
