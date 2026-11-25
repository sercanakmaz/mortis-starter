using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Boilerplate.Shared.Domain;
using System.Text.Json;

namespace Boilerplate.Infrastructure.Persistence;

public static class LocalizedTextConversion
{
    public static readonly ValueConverter<LocalizedText, string> Converter = new(text => Write(text), json => Read(json));

    public static readonly ValueComparer<LocalizedText> Comparer = new(
        (left, right) => Write(left) == Write(right),
        text => Write(text).GetHashCode(),
        text => Read(Write(text)));

    public static string Write(LocalizedText text)
    {
        return text is null ? null : JsonSerializer.Serialize(new SortedDictionary<string, string>(text.Texts.ToDictionary()));
    }

    public static LocalizedText Read(string json)
    {
        return json is null ? null : LocalizedText.FromTexts(JsonSerializer.Deserialize<Dictionary<string, string>>(json));
    }
}
