using System.Security.Cryptography;
using System.Text;

namespace Acme.Api.Middlewares;

public static class ErrorReference
{
    public static string Of(Exception exception)
    {
        var place = $"{exception.GetType().FullName}|{exception.TargetSite?.DeclaringType?.FullName}.{exception.TargetSite?.Name}";

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(place)))[..8].ToLowerInvariant();
    }
}
