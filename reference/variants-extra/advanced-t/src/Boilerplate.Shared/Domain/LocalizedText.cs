namespace Boilerplate.Shared.Domain;

public sealed class LocalizedText
{
    private readonly Dictionary<string, string> _texts;

    private LocalizedText(Dictionary<string, string> texts)
    {
        _texts = texts;
    }

    public IReadOnlyDictionary<string, string> Texts => _texts;

    public static LocalizedText Of(string language, string text)
    {
        return new LocalizedText(new Dictionary<string, string> { [language] = text });
    }

    public static LocalizedText FromTexts(IReadOnlyDictionary<string, string> texts)
    {
        return new LocalizedText(new Dictionary<string, string>(texts));
    }

    public LocalizedText With(string language, string text)
    {
        return new LocalizedText(new Dictionary<string, string>(_texts) { [language] = text });
    }

    public string Get(string language, string fallbackLanguage)
    {
        if (language is not null && _texts.TryGetValue(language, out var text)) return text;

        return _texts.TryGetValue(fallbackLanguage, out var fallback) ? fallback : _texts.Values.FirstOrDefault();
    }
}
