namespace Boilerplate.Infrastructure.Secrets;

public static class SecretsConstants
{
    public const string Version = "v1";
    public const string SecretAnnotation = "Mortis:Secret";
    public const int KeyBytes = 32;
    public const int NonceBytes = 12;
    public const int TagBytes = 16;
    public const int EnvelopeMaxLength = 2048;
}
