namespace Boilerplate.Shared.Domain;

public interface IMultiTenant
{
    string TenantId { get; }
}
