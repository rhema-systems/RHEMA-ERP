using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace ErpSystem.Api.HealthChecks
{
    public class RedisHealthCheck : IHealthCheck
    {
        private readonly RedisHealthCheckOptions _options;
        private readonly ILogger<RedisHealthCheck> _logger;

        public RedisHealthCheck(IOptions<RedisHealthCheckOptions> options, ILogger<RedisHealthCheck> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                using var connection = await ConnectionMultiplexer.ConnectAsync(_options.ConnectionString);
                var database = connection.GetDatabase();
                var testKey = $"health_check_{Guid.NewGuid()}";
                await database.StringSetAsync(testKey, "test", TimeSpan.FromSeconds(10));
                var result = await database.StringGetAsync(testKey);
                await database.KeyDeleteAsync(testKey);

                return result == "test"
                    ? HealthCheckResult.Healthy("Redis is responding")
                    : HealthCheckResult.Degraded("Redis test failed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Redis health check failed");
                return HealthCheckResult.Unhealthy($"Redis connection failed: {ex.Message}");
            }
        }
    }

    public class RedisHealthCheckOptions
    {
        public string ConnectionString { get; set; } = string.Empty;
    }
}
