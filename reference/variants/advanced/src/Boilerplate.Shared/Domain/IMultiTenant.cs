namespace Boilerplate.Shared.Domain;

public interface IMultiTenant
{
    Guid TenantId { get; }
}
