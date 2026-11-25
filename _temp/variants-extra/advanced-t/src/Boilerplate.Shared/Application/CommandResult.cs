namespace Boilerplate.Shared.Application;

public class CommandResult
{
    public Dictionary<string, object> Meta { get; set; }

    public static CommandResult<TContent> FromContent<TContent>(TContent content)
    {
        return new CommandResult<TContent>(content);
    }
}

public sealed class CommandResult<TContent>(TContent content) : CommandResult
{
    public TContent Content { get; } = content;
}
