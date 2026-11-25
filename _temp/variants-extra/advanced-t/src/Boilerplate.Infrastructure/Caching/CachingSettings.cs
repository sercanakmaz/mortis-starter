namespace Boilerplate.Infrastructure.Caching;

public class CachingSettings
{
    public const string Section = "Caching";
    public const string RedisConnectionStringName = "Redis";

    public int Seconds { get; set; } = 60;
}
