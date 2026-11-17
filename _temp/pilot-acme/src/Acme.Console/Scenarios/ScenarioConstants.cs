using System.Text.Json;

namespace Acme.Console.Scenarios;

public static class ScenarioConstants
{
    public const string UserId = "console";

    public const string UserEmail = "console@localhost";

    public const string CommandSuffix = "Command";

    public const string FilePrefix = "@";

    public static readonly JsonSerializerOptions OutputOptions = new() { WriteIndented = true };

    public static readonly JsonSerializerOptions InputOptions = new() { PropertyNameCaseInsensitive = true };
}
