using System.Security.Claims;
using System.Text;
using Acme.Shared.Domain;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Acme.Infrastructure.Authentication;

public class AuthSettings
{
    public const string Section = "Auth";

    public const string MetadataPath = "/.well-known/openid-configuration";

    public string Authority { get; set; }

    public string Issuer { get; set; }

    public string Audience { get; set; }

    public string SigningKey { get; set; }

    public string SubjectClaim { get; set; } = "sub";

    public string EmailClaim { get; set; } = "email";

    public string LanguageClaim { get; set; } = "locale";

    public TokenValidationParameters ValidationParameters()
    {
        return new TokenValidationParameters
        {
            ValidIssuer = Issuer,
            ValidAudience = Audience,
            IssuerSigningKey = string.IsNullOrEmpty(SigningKey) ? null : SigningSecurityKey(),
            ConfigurationManager = string.IsNullOrEmpty(Authority) ? null : new ConfigurationManager<OpenIdConnectConfiguration>(Authority.TrimEnd('/') + MetadataPath, new OpenIdConnectConfigurationRetriever(), new HttpDocumentRetriever()),
            NameClaimType = SubjectClaim
        };
    }

    public SymmetricSecurityKey SigningSecurityKey()
    {
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey));
    }

    public AuditUser UserOf(ClaimsPrincipal principal)
    {
        return new AuditUser(principal.FindFirst(SubjectClaim)?.Value, principal.FindFirst(EmailClaim)?.Value);
    }

    public string LanguageOf(ClaimsPrincipal principal)
    {
        return principal.FindFirst(LanguageClaim)?.Value;
    }
}
