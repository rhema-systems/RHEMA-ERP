namespace ErpSystem.Core.Services
{
    public interface IRateLimitingService
    {
        Task<RateLimitResult> CheckRateLimitAsync(RateLimitRequest request, CancellationToken cancellationToken = default);
        Task ResetUserLimitsAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<RateLimitStats> GetUserStatsAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<RateLimitStats> GetDataSourceStatsAsync(Guid dataSourceId, CancellationToken cancellationToken = default);
    }

    public class RateLimitRequest
    {
        public Guid UserId { get; set; }
        public Guid TenantId { get; set; }
        public Guid DataSourceId { get; set; }
        public RateLimitType Type { get; set; }
        public int RequestedWeight { get; set; } = 1;
    }

    public class RateLimitResult
    {
        public bool IsAllowed { get; set; }
        public string? ReasonCode { get; set; }
        public string? ReasonMessage { get; set; }
        public TimeSpan RetryAfter { get; set; }
        public int RemainingRequests { get; set; }
        public DateTime ResetTime { get; set; }
        public RateLimitType LimitType { get; set; }
    }

    public class RateLimitStats
    {
        public int RequestsInLastMinute { get; set; }
        public int RequestsInLastHour { get; set; }
        public int RequestsInLastDay { get; set; }
        public int ConcurrentQueries { get; set; }
        public TimeSpan TotalQueryTime { get; set; }
        public DateTime FirstRequestToday { get; set; }
        public DateTime LastRequestTime { get; set; }
        public Dictionary<RateLimitType, int> LimitsByType { get; set; } = new();
    }

    public class RateLimitConfiguration
    {
        public Dictionary<RateLimitType, RateLimitRule> Rules { get; set; } = new();
        public Dictionary<string, RateLimitRule> TenantOverrides { get; set; } = new(); // Tenant-specific limits
        public bool EnableRateLimiting { get; set; } = true;
    }

    public class RateLimitRule
    {
        public int RequestsPerMinute { get; set; }
        public int RequestsPerHour { get; set; }
        public int RequestsPerDay { get; set; }
        public int MaxConcurrentQueries { get; set; }
        public TimeSpan MaxQueryDuration { get; set; }
        public int MaxRowsPerQuery { get; set; }
        public long MaxMemoryUsageBytes { get; set; }
        public int Weight { get; set; } = 1; // How much this request "costs"
    }

    public enum RateLimitType
    {
        QueryExecution,
        ReportGeneration,
        DataExport,
        SchemaInspection,
        ConnectionTest,
        TemplateCreation
    }

    // Default configurations for different user roles
    public static class DefaultRateLimits
    {
        public static readonly Dictionary<string, RateLimitConfiguration> ByRole = new()
        {
            ["User"] = new RateLimitConfiguration
            {
                Rules = new Dictionary<RateLimitType, RateLimitRule>
                {
                    [RateLimitType.QueryExecution] = new()
                    {
                        RequestsPerMinute = 10,
                        RequestsPerHour = 100,
                        RequestsPerDay = 500,
                        MaxConcurrentQueries = 2,
                        MaxQueryDuration = TimeSpan.FromMinutes(2),
                        MaxRowsPerQuery = 5_000,
                        MaxMemoryUsageBytes = 50_000_000 // 50MB
                    },
                    [RateLimitType.ReportGeneration] = new()
                    {
                        RequestsPerMinute = 5,
                        RequestsPerHour = 50,
                        RequestsPerDay = 200,
                        MaxConcurrentQueries = 1,
                        MaxQueryDuration = TimeSpan.FromMinutes(5),
                        MaxRowsPerQuery = 10_000,
                        MaxMemoryUsageBytes = 100_000_000 // 100MB
                    }
                }
            },
            ["Admin"] = new RateLimitConfiguration
            {
                Rules = new Dictionary<RateLimitType, RateLimitRule>
                {
                    [RateLimitType.QueryExecution] = new()
                    {
                        RequestsPerMinute = 30,
                        RequestsPerHour = 500,
                        RequestsPerDay = 2000,
                        MaxConcurrentQueries = 5,
                        MaxQueryDuration = TimeSpan.FromMinutes(10),
                        MaxRowsPerQuery = 50_000,
                        MaxMemoryUsageBytes = 200_000_000 // 200MB
                    }
                }
            },
            ["SuperAdmin"] = new RateLimitConfiguration
            {
                Rules = new Dictionary<RateLimitType, RateLimitRule>
                {
                    [RateLimitType.QueryExecution] = new()
                    {
                        RequestsPerMinute = 100,
                        RequestsPerHour = 2000,
                        RequestsPerDay = 10000,
                        MaxConcurrentQueries = 10,
                        MaxQueryDuration = TimeSpan.FromMinutes(30),
                        MaxRowsPerQuery = 100_000,
                        MaxMemoryUsageBytes = 500_000_000 // 500MB
                    }
                }
            }
        };
    }
}
