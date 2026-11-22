namespace Boilerplate.Shared.Application;

public static class Languages
{
    public const string Default = "en";

    public static readonly IReadOnlyList<string> Supported = ["en", "tr"];

    public static string Resolve(IEnumerable<string> candidates)
    {
        foreach (var candidate in candidates.Where(candidate => !string.IsNullOrWhiteSpace(candidate)))
        {
            var language = Supported.FirstOrDefault(supported => string.Equals(supported, candidate, StringComparison.OrdinalIgnoreCase))
                ?? Supported.FirstOrDefault(supported => string.Equals(supported, candidate.Split('-')[0], StringComparison.OrdinalIgnoreCase));

            if (language is not null) return language;
        }

        return Default;
    }
}
