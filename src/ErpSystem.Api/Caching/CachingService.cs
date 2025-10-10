using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;

namespace ErpSystem.Api.Caching;

/// <summary>
/// Multi-tier caching service with L1 (Memory) and L2 (Redis) cache layers
/// </summary>
public interface ICachingService
{
    // Get operations
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    T? Get<T>(string key);
    Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null, CancellationToken cancellationToken = default);
    
    // Set operations
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default);
    void Set<T>(string key, T value, TimeSpan? expiry = null);
    Task SetManyAsync<T>(Dictionary<string, T> items, TimeSpan? expiry = null, CancellationToken cancellationToken = default);
    
    // Remove operations
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    void Remove(string key);
    Task RemovePatternAsync(string pattern, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
    
    // Utilities
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
    Task<TimeSpan?> GetTtlAsync(string key, CancellationToken cancellationToken = default);
    Task RefreshAsync(string key, CancellationToken cancellationToken = default);
}

public class CachingService : ICachingService
{
    private readonly IMemoryCache _memoryCache;
    private readonly IDistributedCache _distributedCache;
    private readonly IConnectionMultiplexer _redis;
    private readonly IDatabase _redisDb;
    private readonly ILogger<CachingService> _logger;
    private readonly CachingOptions _options;

    // L1 Cache (Memory) configuration
    private readonly MemoryCacheEntryOptions _l1DefaultOptions;
    
    public CachingService(
        IMemoryCache memoryCache,
        IDistributedCache distributedCache,
        IConnectionMultiplexer redis,
        ILogger<CachingService> logger,
        Microsoft.Extensions.Options.IOptions<CachingOptions> options)
    {
        _memoryCache = memoryCache;
        _distributedCache = distributedCache;
        _redis = redis;
        _redisDb = redis?.GetDatabase() ?? throw new ArgumentNullException(nameof(redis));
        _logger = logger;
        _options = options.Value;

        _l1DefaultOptions = new MemoryCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromMinutes(5),
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15),
            Priority = CacheItemPriority.Normal
        };
    }

    #region Get Operations

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            // Try L1 cache (Memory) first
            if (_memoryCache.TryGetValue(key, out T? cachedValue))
            {
                _logger.LogDebug("Cache hit (L1) for key: {Key}", key);
                return cachedValue;
            }

            // Try L2 cache (Redis)
            var serializedValue = await _distributedCache.GetStringAsync(key, cancellationToken);
            if (!string.IsNullOrEmpty(serializedValue))
            {
                var value = JsonSerializer.Deserialize<T>(serializedValue);
                
                // Store in L1 cache for faster future access
                _memoryCache.Set(key, value, _l1DefaultOptions);
                
                _logger.LogDebug("Cache hit (L2) for key: {Key}", key);
                return value;
            }

            _logger.LogDebug("Cache miss for key: {Key}", key);
            return default;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving from cache for key: {Key}", key);
            return default;
        }
    }

    public T? Get<T>(string key)
    {
        try
        {
            // Only check L1 cache for synchronous operations
            if (_memoryCache.TryGetValue(key, out T? cachedValue))
            {
                return cachedValue;
            }

            return default;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving from L1 cache for key: {Key}", key);
            return default;
        }
    }

    public async Task<T> GetOrCreateAsync<T>(
        string key, 
        Func<Task<T>> factory, 
        TimeSpan? expiry = null, 
        CancellationToken cancellationToken = default)
    {
        var cached = await GetAsync<T>(key, cancellationToken);
        if (cached != null)
        {
            return cached;
        }

        // Generate value
        var value = await factory();
        
        // Cache the result
        await SetAsync(key, value, expiry, cancellationToken);
        
        return value;
    }

    #endregion

    #region Set Operations

    public async Task SetAsync<T>(
        string key, 
        T value, 
        TimeSpan? expiry = null, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var actualExpiry = expiry ?? _options.DefaultExpiry;
            
            // Set in L1 cache (Memory)
            var l1Options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = actualExpiry,
                SlidingExpiration = actualExpiry > TimeSpan.FromMinutes(10) 
                    ? TimeSpan.FromMinutes(5) 
                    : actualExpiry / 2,
                Priority = CacheItemPriority.Normal
            };
            _memoryCache.Set(key, value, l1Options);

            // Set in L2 cache (Redis)
            var serializedValue = JsonSerializer.Serialize(value);
            var distributedOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = actualExpiry
            };
            
            await _distributedCache.SetStringAsync(key, serializedValue, distributedOptions, cancellationToken);
            
            _logger.LogDebug("Set cache for key: {Key}, expiry: {Expiry}", key, actualExpiry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cache for key: {Key}", key);
        }
    }

    public void Set<T>(string key, T value, TimeSpan? expiry = null)
    {
        try
        {
            var actualExpiry = expiry ?? _options.DefaultExpiry;
            
            var l1Options = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = actualExpiry,
                Priority = CacheItemPriority.Normal
            };
            
            _memoryCache.Set(key, value, l1Options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting L1 cache for key: {Key}", key);
        }
    }

    public async Task SetManyAsync<T>(
        Dictionary<string, T> items, 
        TimeSpan? expiry = null, 
        CancellationToken cancellationToken = default)
    {
        var tasks = items.Select(item => SetAsync(item.Key, item.Value, expiry, cancellationToken));
        await Task.WhenAll(tasks);
    }

    #endregion

    #region Remove Operations

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            // Remove from L1 cache
            _memoryCache.Remove(key);
            
            // Remove from L2 cache
            await _distributedCache.RemoveAsync(key, cancellationToken);
            
            _logger.LogDebug("Removed cache for key: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache for key: {Key}", key);
        }
    }

    public void Remove(string key)
    {
        try
        {
            _memoryCache.Remove(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing L1 cache for key: {Key}", key);
        }
    }

    public async Task RemovePatternAsync(string pattern, CancellationToken cancellationToken = default)
    {
        try
        {
            var server = _redis.GetServer(_redis.GetEndPoints()[0]);
            var keys = server.Keys(pattern: pattern);
            
            foreach (var key in keys)
            {
                await RemoveAsync(key, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache pattern: {Pattern}", pattern);
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Clear L1 cache
            if (_memoryCache is MemoryCache mc)
            {
                mc.Clear();
            }
            
            // Clear L2 cache (Redis) - Use with caution in production!
            var server = _redis.GetServer(_redis.GetEndPoints()[0]);
            await server.FlushDatabaseAsync();
            
            _logger.LogWarning("Cache cleared completely");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing cache");
        }
    }

    #endregion

    #region Utilities

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check L1 first
            if (_memoryCache.TryGetValue(key, out _))
                return true;

            // Check L2
            return await _redisDb.KeyExistsAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking cache existence for key: {Key}", key);
            return false;
        }
    }

    public async Task<TimeSpan?> GetTtlAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _redisDb.KeyTimeToLiveAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting TTL for key: {Key}", key);
            return null;
        }
    }

    public async Task RefreshAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _distributedCache.RefreshAsync(key, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing cache for key: {Key}", key);
        }
    }

    #endregion
}

/// <summary>
/// Caching service configuration options
/// </summary>
public class CachingOptions
{
    public const string SectionName = "Caching";

    /// <summary>
    /// Default cache expiry time
    /// </summary>
    public TimeSpan DefaultExpiry { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// L1 (Memory) cache size limit in MB
    /// </summary>
    public long L1CacheSizeLimitMB { get; set; } = 100;

    /// <summary>
    /// Enable L1 (Memory) caching
    /// </summary>
    public bool EnableL1Cache { get; set; } = true;

    /// <summary>
    /// Enable L2 (Redis) caching
    /// </summary>
    public bool EnableL2Cache { get; set; } = true;

    /// <summary>
    /// Cache key prefix for multi-tenant isolation
    /// </summary>
    public string KeyPrefix { get; set; } = "erp:";

    /// <summary>
    /// Enable cache compression for large objects
    /// </summary>
    public bool EnableCompression { get; set; } = true;

    /// <summary>
    /// Compression threshold in bytes
    /// </summary>
    public int CompressionThreshold { get; set; } = 1024;
}

/// <summary>
/// Cache key helper for consistent key generation
/// </summary>
public static class CacheKeys
{
    public const string USER_PREFIX = "user:";
    public const string TENANT_PREFIX = "tenant:";
    public const string AUDIT_PREFIX = "audit:";
    public const string SETTINGS_PREFIX = "settings:";
    
    public static string User(Guid userId) => $"{USER_PREFIX}{userId}";
    public static string UserTenants(Guid userId) => $"{USER_PREFIX}{userId}:tenants";
    public static string Tenant(Guid tenantId) => $"{TENANT_PREFIX}{tenantId}";
    public static string TenantUsers(Guid tenantId) => $"{TENANT_PREFIX}{tenantId}:users";
    public static string Settings(Guid tenantId, string key) => $"{SETTINGS_PREFIX}{tenantId}:{key}";
    public static string AuditLogs(Guid userId, int page) => $"{AUDIT_PREFIX}{userId}:page:{page}";
}