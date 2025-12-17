using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ErpSystem.Api.Services
{
    public interface IRedisService
    {
        Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
        Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default);
        Task RemoveAsync(string key, CancellationToken cancellationToken = default);
        Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
        Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
        Task<Dictionary<string, string>> GetInfoAsync(CancellationToken cancellationToken = default);
    }

    public class RedisService : IRedisService
    {
        private readonly IDistributedCache _distributedCache;
        private readonly IConnectionMultiplexer? _connectionMultiplexer;
        private readonly ILogger<RedisService> _logger;
        private readonly JsonSerializerOptions _jsonOptions;

        public RedisService(
            IDistributedCache distributedCache,
            ILogger<RedisService> logger,
            IConnectionMultiplexer? connectionMultiplexer = null)
        {
            _distributedCache = distributedCache;
            _connectionMultiplexer = connectionMultiplexer;
            _logger = logger;
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
        }

        public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                var value = await _distributedCache.GetStringAsync(key, cancellationToken);
                if (string.IsNullOrEmpty(value))
                {
                    return default;
                }

                return JsonSerializer.Deserialize<T>(value, _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting value from cache for key: {Key}", key);
                return default;
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var serializedValue = JsonSerializer.Serialize(value, _jsonOptions);
                var options = new DistributedCacheEntryOptions();

                if (expiry.HasValue)
                {
                    options.SetAbsoluteExpiration(expiry.Value);
                }
                else
                {
                    options.SetAbsoluteExpiration(TimeSpan.FromMinutes(30)); // Default 30 minutes
                }

                await _distributedCache.SetStringAsync(key, serializedValue, options, cancellationToken);

                _logger.LogDebug("Set cache value for key: {Key} with expiry: {Expiry}", key, expiry ?? TimeSpan.FromMinutes(30));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting cache value for key: {Key}", key);
            }
        }

        public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                await _distributedCache.RemoveAsync(key, cancellationToken);
                _logger.LogDebug("Removed cache value for key: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cache value for key: {Key}", key);
            }
        }

        public async Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_connectionMultiplexer == null)
                {
                    _logger.LogWarning("Cannot remove by pattern - Redis connection not available");
                    return;
                }

                var database = _connectionMultiplexer.GetDatabase();
                var server = _connectionMultiplexer.GetServer(_connectionMultiplexer.GetEndPoints().First());

                var keys = server.Keys(pattern: pattern);
                var keyArray = keys.ToArray();

                if (keyArray.Any())
                {
                    await database.KeyDeleteAsync(keyArray);
                    _logger.LogDebug("Removed {Count} cache keys matching pattern: {Pattern}", keyArray.Length, pattern);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cache keys by pattern: {Pattern}", pattern);
            }
        }

        public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_connectionMultiplexer == null)
                {
                    // Fallback to trying to get the value
                    var value = await _distributedCache.GetStringAsync(key, cancellationToken);
                    return !string.IsNullOrEmpty(value);
                }

                var database = _connectionMultiplexer.GetDatabase();
                return await database.KeyExistsAsync(key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if key exists: {Key}", key);
                return false;
            }
        }

        public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (_connectionMultiplexer == null)
                {
                    // Test with distributed cache
                    var testKey1 = $"health_check_{Guid.NewGuid()}";
                    var testValue1 = "test";

                    await _distributedCache.SetStringAsync(testKey1, testValue1,
                        new DistributedCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromSeconds(10)),
                        cancellationToken);

                    var retrievedValue1 = await _distributedCache.GetStringAsync(testKey1, cancellationToken);
                    await _distributedCache.RemoveAsync(testKey1, cancellationToken);

                    return testValue1 == retrievedValue1;
                }

                var database = _connectionMultiplexer.GetDatabase();
                var testKey2 = $"health_check_{Guid.NewGuid()}";
                var testValue2 = "test";

                await database.StringSetAsync(testKey2, testValue2, TimeSpan.FromSeconds(10));
                var retrievedValue2 = await database.StringGetAsync(testKey2);
                await database.KeyDeleteAsync(testKey2);

                return testValue2 == retrievedValue2;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redis health check failed");
                return false;
            }
        }

        public async Task<Dictionary<string, string>> GetInfoAsync(CancellationToken cancellationToken = default)
        {
            var info = new Dictionary<string, string>();

            try
            {
                if (_connectionMultiplexer == null)
                {
                    info["Type"] = "DistributedMemoryCache";
                    info["Status"] = "Available";
                    return info;
                }

                info["Type"] = "Redis";
                info["Status"] = _connectionMultiplexer.IsConnected ? "Connected" : "Disconnected";

                var server = _connectionMultiplexer.GetServer(_connectionMultiplexer.GetEndPoints().First());
                var redisInfo = await server.InfoAsync();

                foreach (var group in redisInfo)
                {
                    foreach (var item in group)
                    {
                        if (item.Key.Contains("version") ||
                            item.Key.Contains("memory") ||
                            item.Key.Contains("clients") ||
                            item.Key.Contains("uptime"))
                        {
                            info[item.Key] = item.Value;
                        }
                    }
                }

                var database = _connectionMultiplexer.GetDatabase();
                info["DatabaseSize"] = (await server.DatabaseSizeAsync()).ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Redis info");
                info["Error"] = ex.Message;
            }

            return info;
        }
    }
}
