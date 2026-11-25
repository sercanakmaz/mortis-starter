using Acme.Shared.Application;
using Acme.Shared.Domain;
using Acme.Shared.Exceptions;

namespace Acme.Console;

public record ConsoleArguments(CommandContext CommandContext, string Name, string Argument)
{
    private static readonly string[] Options = [ConsoleConstants.UserOption, ConsoleConstants.EmailOption, ConsoleConstants.TenantOption, ConsoleConstants.LanguageOption];

    public static ConsoleArguments Parse(IReadOnlyList<string> args)
    {
        var options = new Dictionary<string, string>();
        var index = 0;

        while (index < args.Count && args[index].StartsWith(ConsoleConstants.OptionPrefix))
        {
            if (!Options.Contains(args[index]) || index + 1 >= args.Count)
            {
                throw new InvalidParameterException(args[index], $"the options are {string.Join(", ", Options)}, each with a value");
            }

            options[args[index]] = args[index + 1];
            index += 2;
        }

        if (index >= args.Count)
        {
            throw new InvalidParameterException("command", "a command name is required");
        }

        if (!options.TryGetValue(ConsoleConstants.UserOption, out var userId) || string.IsNullOrWhiteSpace(userId))
        {
            throw new InvalidParameterException(ConsoleConstants.UserOption, "the user running the command is required");
        }

        var commandContext = new CommandContext
        {
            User = new AuditUser(userId, options.GetValueOrDefault(ConsoleConstants.EmailOption)),
            TenantId = options.GetValueOrDefault(ConsoleConstants.TenantOption),
            Language = options.GetValueOrDefault(ConsoleConstants.LanguageOption)
        };

        return new ConsoleArguments(commandContext, args[index], args.ElementAtOrDefault(index + 1));
    }
}
