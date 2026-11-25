using Boilerplate.Shared.Application;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Boilerplate.Infrastructure.Secrets;

public static class SecretPropertyExtensions
{
    public static PropertyBuilder<string> IsSecret(this PropertyBuilder<string> propertyBuilder)
    {
        return propertyBuilder.HasAnnotation(SecretsConstants.SecretAnnotation, true).HasMaxLength(SecretsConstants.EnvelopeMaxLength);
    }

    public static void ProtectSecrets(this ModelBuilder modelBuilder, ISecretProtector secretProtector)
    {
        var converter = new ValueConverter<string, string>(plaintext => secretProtector.Protect(plaintext), envelope => secretProtector.Unprotect(envelope));

        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(entityType => entityType.GetProperties()).Where(property => property.FindAnnotation(SecretsConstants.SecretAnnotation) is not null))
        {
            property.SetValueConverter(converter);
        }
    }
}
