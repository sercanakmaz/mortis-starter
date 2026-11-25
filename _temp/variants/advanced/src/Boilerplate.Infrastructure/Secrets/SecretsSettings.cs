namespace Boilerplate.Infrastructure.Secrets;

public class SecretsSettings
{
    public const string Section = "Secrets";

    public string CurrentKeyId { get; set; }
    public Dictionary<string, string> Keys { get; set; } = [];

    public bool HasUsableCurrentKey()
    {
        return CurrentKeyId is not null && Keys.TryGetValue(CurrentKeyId, out var key) && Convert.TryFromBase64String(key, new byte[SecretsConstants.KeyBytes], out var length) && length == SecretsConstants.KeyBytes;
    }
}
