using Boilerplate.Shared.Application;
using System.Globalization;
using System.Text.Json;

namespace Boilerplate.Infrastructure.Localization;

public class MessageLocalizer
{
    private readonly Dictionary<string, Dictionary<string, string>> _messages = Languages.Supported.ToDictionary(language => language, Load);

    public IReadOnlyDictionary<string, string> MessagesOf(string language)
    {
        return _messages[language];
    }

    public string Get(string code, IReadOnlyDictionary<string, object> parameters, string language)
    {
        var template = Find(language, code) ?? Find(Languages.Default, code);

        return template is null ? null : parameters.Aggregate(template, (text, parameter) => text.Replace("{" + parameter.Key + "}", Convert.ToString(parameter.Value, CultureInfo.InvariantCulture)));
    }

    private string Find(string language, string code)
    {
        return language is not null && _messages.TryGetValue(language, out var messages) && messages.TryGetValue(code, out var template) ? template : null;
    }

    private static Dictionary<string, string> Load(string language)
    {
        using var stream = typeof(MessageLocalizer).Assembly.GetManifestResourceStream($"{typeof(MessageLocalizer).Namespace}.messages.{language}.json");

        return stream is null ? [] : JsonSerializer.Deserialize<Dictionary<string, string>>(stream);
    }
}
