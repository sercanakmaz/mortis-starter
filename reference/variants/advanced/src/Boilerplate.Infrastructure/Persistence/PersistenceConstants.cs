namespace Boilerplate.Infrastructure.Persistence;

public static class PersistenceConstants
{
    public const string ConnectionStringName = "Default";
    public const string ConnectionStringVariable = "ConnectionStrings__Default";
    public const string DesignTimeConnectionString = "Host=localhost;Port=5432;Database=boilerplate;Username=postgres;Password=postgres";
    public const int UserIdMaxLength = 64;
    public const int UserNameMaxLength = 256;
    public const string RowVersionColumn = "xmin";
    public const string TenantOwnedData = "Tenant-owned data";
    public const string TenantFilter = "Tenant";
    public const string SoftDeleteFilter = "SoftDelete";
}
