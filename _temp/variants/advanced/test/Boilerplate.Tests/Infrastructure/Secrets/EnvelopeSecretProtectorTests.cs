using System.Security.Cryptography;
using Boilerplate.Infrastructure.Secrets;
using Boilerplate.Tests.TestSupport.Infrastructure.Secrets;
using Microsoft.Extensions.Options;

namespace Boilerplate.Tests.Infrastructure.Secrets;

public class EnvelopeSecretProtectorTests
{
    [Fact]
    public void Protect_ThenUnprotect_Should_GiveBackThePlaintextFromAV1EnvelopeNamingTheCurrentKey()
    {
        var secretProtector = new EnvelopeSecretProtector(Options.Create(FakeSecretsSettings.Create()));

        var envelope = secretProtector.Protect("TR-123");

        Assert.StartsWith($"{SecretsConstants.Version}.{FakeSecretsSettings.FirstKeyId}.", envelope);
        Assert.DoesNotContain("TR-123", envelope);
        Assert.Equal("TR-123", secretProtector.Unprotect(envelope));
    }

    [Fact]
    public void Protect_TwiceForOneValue_Should_GiveDifferentEnvelopes()
    {
        var secretProtector = new EnvelopeSecretProtector(Options.Create(FakeSecretsSettings.Create()));

        Assert.NotEqual(secretProtector.Protect("TR-123"), secretProtector.Protect("TR-123"));
    }

    [Fact]
    public void Unprotect_AfterTheCurrentKeyChanged_Should_StillOpenAnEnvelopeOfTheOldKey()
    {
        var envelope = new EnvelopeSecretProtector(Options.Create(FakeSecretsSettings.Create(FakeSecretsSettings.FirstKeyId))).Protect("TR-123");

        var plaintext = new EnvelopeSecretProtector(Options.Create(FakeSecretsSettings.Create(FakeSecretsSettings.SecondKeyId))).Unprotect(envelope);

        Assert.Equal("TR-123", plaintext);
    }

    [Fact]
    public void Unprotect_WithAChangedCiphertext_Should_Throw()
    {
        var secretProtector = new EnvelopeSecretProtector(Options.Create(FakeSecretsSettings.Create()));
        var parts = secretProtector.Protect("TR-123").Split('.');
        parts[4] = parts[4][0] == 'A' ? 'B' + parts[4][1..] : 'A' + parts[4][1..];

        Assert.ThrowsAny<CryptographicException>(() => secretProtector.Unprotect(string.Join('.', parts)));
    }

    [Fact]
    public void ProtectAndUnprotect_WithNull_Should_ReturnNull()
    {
        var secretProtector = new EnvelopeSecretProtector(Options.Create(FakeSecretsSettings.Create()));

        Assert.Equal((null, null), (secretProtector.Protect(null), secretProtector.Unprotect(null)));
    }

    [Theory]
    [InlineData("test-1", true)]
    [InlineData("missing", false)]
    [InlineData(null, false)]
    public void HasUsableCurrentKey_Should_RequireAThirtyTwoByteKeyUnderTheCurrentId(string currentKeyId, bool expected)
    {
        Assert.Equal(expected, FakeSecretsSettings.Create(currentKeyId).HasUsableCurrentKey());
    }
}
