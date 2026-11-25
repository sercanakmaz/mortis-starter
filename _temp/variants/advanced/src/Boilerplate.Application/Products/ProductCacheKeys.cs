namespace Boilerplate.Application.Products;

public static class ProductCacheKeys
{
    public static string ById(string tenantId, Guid id, string language) => $"products:{tenantId}:{id}:{language}";

    public static string Tag(string tenantId, string id) => $"product:{tenantId}:{id}";
}
