using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Boilerplate.Shared.Application;
using Microsoft.Extensions.Options;

namespace Boilerplate.Infrastructure.Secrets;

public class EnvelopeSecretProtector(IOptions<SecretsSettings> settings) : ISecretProtector
{
    public string Protect(string plaintext)
    {
        if (plaintext is null)
        {
            return null;
        }

        var keyId = settings.Value.CurrentKeyId;
        var dataKey = RandomNumberGenerator.GetBytes(SecretsConstants.KeyBytes);
        var (wrapNonce, wrappedKey, wrapTag) = Seal(KeyOf(keyId), dataKey);
        var (nonce, ciphertext, tag) = Seal(dataKey, Encoding.UTF8.GetBytes(plaintext));

        return string.Join('.', SecretsConstants.Version, keyId, Encode([.. wrapNonce, .. wrappedKey, .. wrapTag]), Encode(nonce), Encode(ciphertext), Encode(tag));
    }

    public string Unprotect(string envelope)
    {
        if (envelope is null)
        {
            return null;
        }

        var parts = envelope.Split('.');

        if (parts.Length != 6 || parts[0] != SecretsConstants.Version)
        {
            throw new InvalidOperationException("The value is not a secret envelope.");
        }

        var wrapped = Base64Url.DecodeFromChars(parts[2]);
        var dataKey = Open(KeyOf(parts[1]), wrapped[..SecretsConstants.NonceBytes], wrapped[SecretsConstants.NonceBytes..^SecretsConstants.TagBytes], wrapped[^SecretsConstants.TagBytes..]);

        return Encoding.UTF8.GetString(Open(dataKey, Base64Url.DecodeFromChars(parts[3]), Base64Url.DecodeFromChars(parts[4]), Base64Url.DecodeFromChars(parts[5])));
    }

    private byte[] KeyOf(string keyId)
    {
        return settings.Value.Keys.TryGetValue(keyId, out var key) ? Convert.FromBase64String(key) : throw new InvalidOperationException($"No secret key {keyId}.");
    }

    private static (byte[] Nonce, byte[] Ciphertext, byte[] Tag) Seal(byte[] key, byte[] plaintext)
    {
        var nonce = RandomNumberGenerator.GetBytes(SecretsConstants.NonceBytes);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[SecretsConstants.TagBytes];

        using var aesGcm = new AesGcm(key, SecretsConstants.TagBytes);
        aesGcm.Encrypt(nonce, plaintext, ciphertext, tag);

        return (nonce, ciphertext, tag);
    }

    private static byte[] Open(byte[] key, byte[] nonce, byte[] ciphertext, byte[] tag)
    {
        var plaintext = new byte[ciphertext.Length];

        using var aesGcm = new AesGcm(key, SecretsConstants.TagBytes);
        aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);

        return plaintext;
    }

    private static string Encode(byte[] bytes) => Base64Url.EncodeToString(bytes);
}
