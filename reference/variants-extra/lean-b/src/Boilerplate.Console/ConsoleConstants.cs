using System.Text.Json;

namespace Boilerplate.Console;

public static class ConsoleConstants
{
    public const string UserOption = "--user";
    public const string EmailOption = "--email";
    public const string TenantOption = "--tenant";
    public const string LanguageOption = "--language";
    public const string OptionPrefix = "--";
    public const string CommandSuffix = "Command";
    public const string FilePrefix = "@";

    public static readonly JsonSerializerOptions OutputOptions = new() { WriteIndented = true };
    public static readonly JsonSerializerOptions InputOptions = new() { PropertyNameCaseInsensitive = true };
}
