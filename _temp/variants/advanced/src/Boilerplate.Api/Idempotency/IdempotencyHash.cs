using System.Security.Cryptography;
using System.Text;

namespace Boilerplate.Api.Idempotency;

public static class IdempotencyHash
{
    public static async Task<string> OfAsync(HttpRequest request)
    {
        request.EnableBuffering();

        using var buffer = new MemoryStream();
        await request.Body.CopyToAsync(buffer);
        request.Body.Position = 0;

        return Convert.ToHexString(SHA256.HashData([.. Encoding.UTF8.GetBytes($"{request.Method} {request.Path}\n"), .. buffer.ToArray()]));
    }
}
