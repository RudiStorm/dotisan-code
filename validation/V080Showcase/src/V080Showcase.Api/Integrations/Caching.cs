using Microsoft.Extensions.Caching.Memory;
namespace V080Showcase.Api.Integrations;

public interface IApplicationCache
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, TimeSpan duration, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}
public interface IDistributedApplicationCache : IApplicationCache { }
public sealed class MemoryApplicationCache(IMemoryCache cache, ILogger<MemoryApplicationCache> logger) : IDistributedApplicationCache
{
    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) { cache.TryGetValue(key, out T? value); logger.LogDebug("Cache {CacheKey} {CacheResult}", key, value is null ? "miss" : "hit"); return Task.FromResult(value); }
    public Task SetAsync<T>(string key, T value, TimeSpan duration, CancellationToken cancellationToken = default) { cache.Set(key, value, duration); return Task.CompletedTask; }
    public Task RemoveAsync(string key, CancellationToken cancellationToken = default) { cache.Remove(key); return Task.CompletedTask; }
}
public static class CacheKeys { public static string ForTenant(string tenantId, string resource, string key) => $"tenant:{tenantId}:{resource}:{key}"; }