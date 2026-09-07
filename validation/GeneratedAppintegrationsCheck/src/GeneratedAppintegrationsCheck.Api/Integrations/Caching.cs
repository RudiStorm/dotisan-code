using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
namespace GeneratedAppintegrationsCheck.Api.Integrations;

public interface IApplicationCache
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, TimeSpan duration, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}
public interface IDistributedApplicationCache : IApplicationCache { }
public sealed partial class MemoryApplicationCache(IMemoryCache cache, ILogger<MemoryApplicationCache> logger) : IDistributedApplicationCache
{
    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) { cache.TryGetValue(key, out T? value); CacheLookup(logger, key, value is null ? "miss" : "hit"); return Task.FromResult(value); }
    public Task SetAsync<T>(string key, T value, TimeSpan duration, CancellationToken cancellationToken = default) { cache.Set(key, value, duration); return Task.CompletedTask; }
    public Task RemoveAsync(string key, CancellationToken cancellationToken = default) { cache.Remove(key); return Task.CompletedTask; }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Cache {CacheKey} {CacheResult}")]
    private static partial void CacheLookup(ILogger logger, string cacheKey, string cacheResult);
}
public static class CacheKeys { public static string ForTenant(string tenantId, string resource, string key) => $"tenant:{tenantId}:{resource}:{key}"; }