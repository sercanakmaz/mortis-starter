using Boilerplate.Infrastructure.Secrets;

namespace Boilerplate.Tests.TestSupport.Infrastructure.Secrets;

public static class FakeSecretsSettings
{
    public const string FirstKeyId = "test-1";
    public const string SecondKeyId = "test-2";
    public const string FirstKey = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8=";
    public const string SecondKey = "HxwdHBsaGRgXFhUUExIREA8ODQwLCgkIBwYFBAMCAQA=";

    public static SecretsSettings Create(string currentKeyId = FirstKeyId) => new()
    {
        CurrentKeyId = currentKeyId,
        Keys = new Dictionary<string, string> { [FirstKeyId] = FirstKey, [SecondKeyId] = SecondKey }
    };
}
