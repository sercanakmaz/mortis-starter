using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Boilerplate.Infrastructure.Persistence.Idempotency;

public class IdempotencyStore(IServiceScopeFactory serviceScopeFactory, IOptions<IdempotencySettings> settings)
{
    public virtual async Task<IdempotencyRecord> StartAsync(string scope, string key, string requestHash)
    {
        using var serviceScope = serviceScopeFactory.CreateScope();
        var appDbContext = serviceScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;
        var takeOverBefore = now.AddSeconds(-settings.Value.InProgressTimeoutSeconds);

        var started = await appDbContext.Database.ExecuteSqlAsync($"""
            insert into idempotency_records (scope, key, request_hash, started_at) values ({scope}, {key}, {requestHash}, {now})
            on conflict (scope, key) do update set request_hash = excluded.request_hash, started_at = excluded.started_at
            where idempotency_records.status_code is null and idempotency_records.started_at < {takeOverBefore}
            """);

        return started == 1
            ? null
            : await appDbContext.Set<IdempotencyRecord>().AsNoTracking().SingleAsync(record => record.Scope == scope && record.Key == key);
    }

    public virtual async Task FinishAsync(string scope, string key, int statusCode, string location, string responseBody)
    {
        using var serviceScope = serviceScopeFactory.CreateScope();
        var appDbContext = serviceScope.ServiceProvider.GetRequiredService<AppDbContext>();

        await appDbContext.Database.ExecuteSqlAsync($"update idempotency_records set status_code = {statusCode}, location = {location}, response_body = {responseBody} where scope = {scope} and key = {key}");
    }

    public virtual async Task ForgetAsync(string scope, string key)
    {
        using var serviceScope = serviceScopeFactory.CreateScope();
        var appDbContext = serviceScope.ServiceProvider.GetRequiredService<AppDbContext>();

        await appDbContext.Database.ExecuteSqlAsync($"delete from idempotency_records where scope = {scope} and key = {key} and status_code is null");
    }

    public virtual async Task<int> DeleteExpiredAsync()
    {
        using var serviceScope = serviceScopeFactory.CreateScope();
        var appDbContext = serviceScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var expiredBefore = DateTime.UtcNow.AddHours(-settings.Value.RetentionHours);

        return await appDbContext.Database.ExecuteSqlAsync($"delete from idempotency_records where started_at < {expiredBefore}");
    }
}
