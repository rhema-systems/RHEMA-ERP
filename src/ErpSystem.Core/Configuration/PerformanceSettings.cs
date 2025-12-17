namespace ErpSystem.Core.Configuration
{
    public class PerformanceSettings
    {
        public ConnectionPoolSettings ConnectionPool { get; set; } = new();
        public QueryCacheSettings QueryCache { get; set; } = new();
        public QueryExecutionSettings QueryExecution { get; set; } = new();
        public ResourceLimitsSettings ResourceLimits { get; set; } = new();
        public MonitoringSettings Monitoring { get; set; } = new();
    }

    public class ConnectionPoolSettings
    {
        public bool EnablePooling { get; set; } = true;
        public int DefaultMinPoolSize { get; set; } = 2;
        public int DefaultMaxPoolSize { get; set; } = 20;
        public int MaxPoolsPerDataSource { get; set; } = 5;
        public TimeSpan ConnectionLifetime { get; set; } = TimeSpan.FromMinutes(30);
        public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(10);
        public TimeSpan AcquisitionTimeout { get; set; } = TimeSpan.FromSeconds(30);
        public bool EnableWarmup { get; set; } = true;
        public int WarmupConnectionCount { get; set; } = 3;
    }

    public class QueryCacheSettings
    {
        public bool EnableCaching { get; set; } = true;
        public string CacheProvider { get; set; } = "Redis"; // Redis, InMemory, Hybrid
        public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromMinutes(15);
        public TimeSpan MaxExpiration { get; set; } = TimeSpan.FromHours(4);
        public long MaxCacheSizeBytes { get; set; } = 1_000_000_000; // 1GB
        public int MaxResultRowsToCache { get; set; } = 10_000;
        public bool CompressLargeResults { get; set; } = true;
        public int CompressionThresholdBytes { get; set; } = 1_000_000; // 1MB
        public bool UseDistributedCache { get; set; } = true;
    }

    public class QueryExecutionSettings
    {
        public bool EnableBackgroundProcessing { get; set; } = true;
        public int MaxConcurrentQueries { get; set; } = 50;
        public int MaxQueuedQueries { get; set; } = 200;
        public TimeSpan DefaultQueryTimeout { get; set; } = TimeSpan.FromMinutes(5);
        public TimeSpan MaxQueryTimeout { get; set; } = TimeSpan.FromMinutes(30);
        public int MaxResultRows { get; set; } = 100_000;
        public long MaxMemoryPerQuery { get; set; } = 200_000_000; // 200MB
        public bool EnableQueryOptimization { get; set; } = true;
        public bool LogSlowQueries { get; set; } = true;
        public TimeSpan SlowQueryThreshold { get; set; } = TimeSpan.FromSeconds(30);
    }

    public class ResourceLimitsSettings
    {
        public long MaxTotalMemoryUsage { get; set; } = 2_000_000_000; // 2GB
        public int MaxConcurrentUsersPerTenant { get; set; } = 100;
        public int MaxDataSourcesPerTenant { get; set; } = 50;
        public int MaxReportsPerUser { get; set; } = 1000;
        public TimeSpan ResourceCleanupInterval { get; set; } = TimeSpan.FromMinutes(5);
        public double CpuThresholdForThrottling { get; set; } = 80.0; // 80% CPU usage
        public double MemoryThresholdForThrottling { get; set; } = 85.0; // 85% memory usage
    }

    public class MonitoringSettings
    {
        public bool EnablePerformanceMonitoring { get; set; } = true;
        public bool EnableDetailedLogging { get; set; } = false;
        public TimeSpan MetricsCollectionInterval { get; set; } = TimeSpan.FromSeconds(30);
        public TimeSpan MetricsRetentionPeriod { get; set; } = TimeSpan.FromDays(7);
        public bool EnableAlerts { get; set; } = true;
        public bool EnableHealthChecks { get; set; } = true;
        public List<string> AlertRecipients { get; set; } = new();
        public Dictionary<string, double> AlertThresholds { get; set; } = new()
        {
            ["QueryExecutionTime"] = 60.0, // seconds
            ["CacheHitRatio"] = 70.0, // percentage
            ["QueueLength"] = 100.0, // number of queued queries
            ["ErrorRate"] = 5.0 // percentage
        };
    }

    // Environment-specific configurations
    public static class PerformanceProfiles
    {
        public static readonly Dictionary<string, PerformanceSettings> Profiles = new()
        {
            ["Development"] = new PerformanceSettings
            {
                ConnectionPool = new ConnectionPoolSettings
                {
                    DefaultMinPoolSize = 1,
                    DefaultMaxPoolSize = 5,
                    EnableWarmup = false
                },
                QueryCache = new QueryCacheSettings
                {
                    DefaultExpiration = TimeSpan.FromMinutes(5),
                    MaxCacheSizeBytes = 100_000_000, // 100MB
                    UseDistributedCache = false
                },
                QueryExecution = new QueryExecutionSettings
                {
                    MaxConcurrentQueries = 10,
                    MaxQueuedQueries = 20,
                    DefaultQueryTimeout = TimeSpan.FromMinutes(2)
                }
            },
            ["Testing"] = new PerformanceSettings
            {
                ConnectionPool = new ConnectionPoolSettings
                {
                    DefaultMinPoolSize = 2,
                    DefaultMaxPoolSize = 10,
                    EnableWarmup = true,
                    WarmupConnectionCount = 2
                },
                QueryCache = new QueryCacheSettings
                {
                    DefaultExpiration = TimeSpan.FromMinutes(10),
                    MaxCacheSizeBytes = 500_000_000, // 500MB
                    UseDistributedCache = true
                },
                QueryExecution = new QueryExecutionSettings
                {
                    MaxConcurrentQueries = 25,
                    MaxQueuedQueries = 100,
                    DefaultQueryTimeout = TimeSpan.FromMinutes(3)
                }
            },
            ["Production"] = new PerformanceSettings
            {
                ConnectionPool = new ConnectionPoolSettings
                {
                    DefaultMinPoolSize = 5,
                    DefaultMaxPoolSize = 50,
                    EnableWarmup = true,
                    WarmupConnectionCount = 5
                },
                QueryCache = new QueryCacheSettings
                {
                    DefaultExpiration = TimeSpan.FromMinutes(30),
                    MaxCacheSizeBytes = 2_000_000_000, // 2GB
                    UseDistributedCache = true,
                    CompressLargeResults = true
                },
                QueryExecution = new QueryExecutionSettings
                {
                    MaxConcurrentQueries = 100,
                    MaxQueuedQueries = 500,
                    DefaultQueryTimeout = TimeSpan.FromMinutes(10),
                    EnableQueryOptimization = true
                },
                Monitoring = new MonitoringSettings
                {
                    EnableDetailedLogging = true,
                    MetricsCollectionInterval = TimeSpan.FromSeconds(10),
                    EnableAlerts = true
                }
            }
        };
    }
}
