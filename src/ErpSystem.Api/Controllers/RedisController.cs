using ErpSystem.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ErpSystem.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "SuperAdmin,TenantAdmin")]
    public class RedisController : ControllerBase
    {
        private readonly IRedisService _redisService;
        private readonly ILogger<RedisController> _logger;

        public RedisController(IRedisService redisService, ILogger<RedisController> logger)
        {
            _redisService = redisService;
            _logger = logger;
        }

        /// <summary>
        /// Get Redis connection information and health status
        /// </summary>
        [HttpGet("info")]
        public async Task<ActionResult<object>> GetInfo()
        {
            try
            {
                var isHealthy = await _redisService.IsHealthyAsync();
                var info = await _redisService.GetInfoAsync();

                return Ok(new
                {
                    IsHealthy = isHealthy,
                    Info = info,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Redis info");
                return StatusCode(500, new { Error = "Failed to get Redis information", Message = ex.Message });
            }
        }

        /// <summary>
        /// Test Redis connectivity by setting and retrieving a test value
        /// </summary>
        [HttpPost("test")]
        public async Task<ActionResult<object>> TestConnection()
        {
            try
            {
                var testKey = $"redis_test_{Guid.NewGuid()}";
                var testValue = new { Message = "Redis test", Timestamp = DateTime.UtcNow };

                // Set test value
                await _redisService.SetAsync(testKey, testValue, TimeSpan.FromMinutes(1));
                _logger.LogInformation("Set Redis test value with key: {TestKey}", testKey);

                // Get test value
                var retrievedValue = await _redisService.GetAsync<object>(testKey);

                // Check if key exists
                var keyExists = await _redisService.ExistsAsync(testKey);

                // Remove test value
                await _redisService.RemoveAsync(testKey);

                return Ok(new
                {
                    TestKey = testKey,
                    SetValue = testValue,
                    RetrievedValue = retrievedValue,
                    KeyExisted = keyExists,
                    Success = retrievedValue != null,
                    Message = retrievedValue != null ? "Redis test successful" : "Redis test failed"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redis test failed");
                return StatusCode(500, new { Error = "Redis test failed", Message = ex.Message });
            }
        }

        /// <summary>
        /// Clear all cache entries (use with caution)
        /// </summary>
        [HttpDelete("clear")]
        public async Task<ActionResult<object>> ClearCache([FromQuery] string? pattern = null)
        {
            try
            {
                if (string.IsNullOrEmpty(pattern))
                {
                    // Clear all ERP system cache entries
                    pattern = "ErpSystem:*";
                }

                await _redisService.RemoveByPatternAsync(pattern);

                _logger.LogWarning("Cleared Redis cache with pattern: {Pattern}", pattern);

                return Ok(new
                {
                    Pattern = pattern,
                    Message = $"Cache cleared for pattern: {pattern}",
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing Redis cache with pattern: {Pattern}", pattern);
                return StatusCode(500, new { Error = "Failed to clear cache", Message = ex.Message });
            }
        }

        /// <summary>
        /// Get cache statistics and health check
        /// </summary>
        [HttpGet("health")]
        public async Task<ActionResult<object>> GetHealth()
        {
            try
            {
                var isHealthy = await _redisService.IsHealthyAsync();
                var info = await _redisService.GetInfoAsync();

                var healthStatus = isHealthy ? "Healthy" : "Unhealthy";
                var statusCode = isHealthy ? 200 : 503;

                var response = new
                {
                    Status = healthStatus,
                    IsHealthy = isHealthy,
                    RedisInfo = info,
                    CheckedAt = DateTime.UtcNow
                };

                return StatusCode(statusCode, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redis health check failed");
                return StatusCode(503, new
                {
                    Status = "Unhealthy",
                    IsHealthy = false,
                    Error = ex.Message,
                    CheckedAt = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Set a cache value manually (for testing)
        /// </summary>
        [HttpPost("set")]
        public async Task<ActionResult<object>> SetCacheValue([FromBody] SetCacheRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Key))
                {
                    return BadRequest(new { Error = "Key is required" });
                }

                var expiry = request.ExpiryMinutes.HasValue
                    ? TimeSpan.FromMinutes(request.ExpiryMinutes.Value)
                    : TimeSpan.FromMinutes(30);

                await _redisService.SetAsync(request.Key, request.Value, expiry);

                _logger.LogInformation("Set cache value for key: {Key} with expiry: {Expiry}", request.Key, expiry);

                return Ok(new
                {
                    Key = request.Key,
                    Value = request.Value,
                    Expiry = expiry,
                    Message = "Cache value set successfully",
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting cache value for key: {Key}", request.Key);
                return StatusCode(500, new { Error = "Failed to set cache value", Message = ex.Message });
            }
        }

        /// <summary>
        /// Get a cache value by key
        /// </summary>
        [HttpGet("get/{key}")]
        public async Task<ActionResult<object>> GetCacheValue(string key)
        {
            try
            {
                if (string.IsNullOrEmpty(key))
                {
                    return BadRequest(new { Error = "Key is required" });
                }

                var value = await _redisService.GetAsync<object>(key);
                var exists = await _redisService.ExistsAsync(key);

                return Ok(new
                {
                    Key = key,
                    Value = value,
                    Exists = exists,
                    Found = value != null,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cache value for key: {Key}", key);
                return StatusCode(500, new { Error = "Failed to get cache value", Message = ex.Message });
            }
        }
    }

    public class SetCacheRequest
    {
        public string Key { get; set; } = string.Empty;
        public object? Value { get; set; }
        public int? ExpiryMinutes { get; set; }
    }
}
