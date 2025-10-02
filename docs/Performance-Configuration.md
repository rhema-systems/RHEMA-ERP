# Performance Configuration Guide

This document details all the configuration settings for the enterprise performance architecture of the Data Sources system.

## Overview

The performance system is highly configurable through several configuration classes that control caching, connection pooling, query execution, rate limiting, and resource management.

## Main Configuration Classes

### PerformanceSettings

The root configuration class that contains all performance-related settings:

```csharp path=null start=null
public class PerformanceSettings
{
    public ConnectionPoolSettings ConnectionPool { get; set; } = new();
    public QueryCacheSettings QueryCache { get; set; } = new();
    public QueryExecutionSettings QueryExecution { get; set; } = new();
    public RateLimitingSettings RateLimiting { get; set; } = new();
    public ResourceLimitSettings ResourceLimits { get; set; } = new();
    public MonitoringSettings Monitoring { get; set; } = new();
}
```

## Connection Pool Configuration

### ConnectionPoolSettings

Controls how database connections are managed and pooled:

```csharp path=null start=null
public class ConnectionPoolSettings
{
    // Pool sizing
    public int DefaultPoolSize { get; set; } = 20;
    public int MaxPoolSize { get; set; } = 100;
    public int MinPoolSize { get; set; } = 5;
    
    // Connection lifecycle
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(10);
    public TimeSpan MaxConnectionAge { get; set; } = TimeSpan.FromHours(1);
    
    // Pool behavior
    public bool EnableWarmup { get; set; } = true;
    public int WarmupConnectionCount { get; set; } = 5;
    public bool EnableHealthCheck { get; set; } = true;
    public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromMinutes(5);
    
    // Performance tuning
    public bool EnablePooling { get; set; } = true;
    public TimeSpan PoolCleanupInterval { get; set; } = TimeSpan.FromMinutes(2);
    public double PoolExpansionFactor { get; set; } = 1.5; // Grow pool by 50% when needed
}
```

### Configuration Example

```json path=null start=null
{
  "PerformanceSettings": {
    "ConnectionPool": {
      "DefaultPoolSize": 25,
      "MaxPoolSize": 150,
      "MinPoolSize": 10,
      "ConnectionTimeout": "00:00:45",
      "IdleTimeout": "00:15:00",
      "MaxConnectionAge": "02:00:00",
      "EnableWarmup": true,
      "WarmupConnectionCount": 10,
      "EnableHealthCheck": true,
      "HealthCheckInterval": "00:03:00"
    }
  }
}
```

## Query Cache Configuration

### QueryCacheSettings

Controls how query results are cached:

```csharp path=null start=null
public class QueryCacheSettings
{
    // Cache behavior
    public bool EnableCaching { get; set; } = true;
    public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromMinutes(15);
    public TimeSpan MaxExpiration { get; set; } = TimeSpan.FromHours(4);
    public TimeSpan MinExpiration { get; set; } = TimeSpan.FromMinutes(1);
    
    // Cache sizing
    public long MaxCacheSize { get; set; } = 500_000_000; // 500MB
    public int MaxResultRowsToCache { get; set; } = 10_000;
    public long MaxIndividualResultSize { get; set; } = 50_000_000; // 50MB
    
    // Performance optimizations
    public bool CompressData { get; set; } = true;
    public bool EnableDistributedCache { get; set; } = true;
    public string RedisConnectionString { get; set; } = "localhost:6379";
    public int RedisDatabaseIndex { get; set; } = 0;
    
    // Cache strategies
    public bool EnableIntelligentExpiration { get; set; } = true;
    public double CacheHitRatioTarget { get; set; } = 0.8; // Target 80% hit ratio
    public TimeSpan CacheKeyExpiration { get; set; } = TimeSpan.FromDays(7);
}
```

### Cache Expiration Rules

The system uses intelligent expiration based on query characteristics:

- **Simple SELECT queries**: 15-30 minutes
- **Aggregation queries**: 1-4 hours  
- **Large result sets (>5000 rows)**: 5-15 minutes
- **Schema information**: 2-24 hours
- **Connection metadata**: 10-60 minutes

### Configuration Example

```json path=null start=null
{
  "PerformanceSettings": {
    "QueryCache": {
      "EnableCaching": true,
      "DefaultExpiration": "00:20:00",
      "MaxExpiration": "06:00:00",
      "MaxCacheSize": 750000000,
      "MaxResultRowsToCache": 15000,
      "CompressData": true,
      "RedisConnectionString": "your-redis-server:6379",
      "RedisDatabaseIndex": 1,
      "EnableIntelligentExpiration": true,
      "CacheHitRatioTarget": 0.85
    }
  }
}
```

## Query Execution Configuration

### QueryExecutionSettings

Controls how queries are executed, including background processing:

```csharp path=null start=null
public class QueryExecutionSettings
{
    // Background processing
    public bool EnableBackgroundProcessing { get; set; } = true;
    public int MaxConcurrentBackgroundQueries { get; set; } = 10;
    public TimeSpan BackgroundQueryTimeout { get; set; } = TimeSpan.FromMinutes(30);
    
    // Query thresholds
    public int BackgroundProcessingRowThreshold { get; set; } = 10_000;
    public int BackgroundProcessingComplexityThreshold { get; set; } = 1000; // characters
    public TimeSpan BackgroundProcessingTimeThreshold { get; set; } = TimeSpan.FromSeconds(30);
    
    // Queue management
    public string JobQueueConnectionString { get; set; } = "DefaultConnection";
    public int QueueWorkerCount { get; set; } = 5;
    public TimeSpan JobRetentionTime { get; set; } = TimeSpan.FromHours(24);
    public int MaxRetryAttempts { get; set; } = 3;
    
    // Result storage
    public string ResultStorageConnectionString { get; set; } = "DefaultConnection";
    public TimeSpan ResultRetentionTime { get; set; } = TimeSpan.FromHours(4);
    public long MaxResultStorageSize { get; set; } = 100_000_000; // 100MB per result
}
```

### Configuration Example

```json path=null start=null
{
  "PerformanceSettings": {
    "QueryExecution": {
      "EnableBackgroundProcessing": true,
      "MaxConcurrentBackgroundQueries": 15,
      "BackgroundQueryTimeout": "00:45:00",
      "BackgroundProcessingRowThreshold": 15000,
      "BackgroundProcessingComplexityThreshold": 1500,
      "QueueWorkerCount": 8,
      "ResultRetentionTime": "06:00:00",
      "MaxRetryAttempts": 2
    }
  }
}
```

## Rate Limiting Configuration

### RateLimitingSettings

Controls rate limiting and resource quotas:

```csharp path=null start=null
public class RateLimitingSettings
{
    // Global rate limits
    public bool EnableRateLimiting { get; set; } = true;
    public int GlobalQueriesPerMinute { get; set; } = 1000;
    public int GlobalConcurrentQueries { get; set; } = 100;
    
    // Per-user limits
    public Dictionary<string, UserRateLimits> RoleBasedLimits { get; set; } = new()
    {
        ["Admin"] = new UserRateLimits { QueriesPerMinute = 100, ConcurrentQueries = 10, MaxRowsPerQuery = 100_000 },
        ["User"] = new UserRateLimits { QueriesPerMinute = 30, ConcurrentQueries = 3, MaxRowsPerQuery = 10_000 },
        ["Viewer"] = new UserRateLimits { QueriesPerMinute = 10, ConcurrentQueries = 2, MaxRowsPerQuery = 5_000 }
    };
    
    // Data source limits
    public int QueriesPerDataSourcePerMinute { get; set; } = 200;
    public int ConcurrentQueriesPerDataSource { get; set; } = 20;
    
    // Circuit breaker
    public bool EnableCircuitBreaker { get; set; } = true;
    public int CircuitBreakerFailureThreshold { get; set; } = 10; // failures in window
    public TimeSpan CircuitBreakerTimeWindow { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan CircuitBreakerRecoveryTime { get; set; } = TimeSpan.FromMinutes(2);
}

public class UserRateLimits
{
    public int QueriesPerMinute { get; set; }
    public int ConcurrentQueries { get; set; }
    public int MaxRowsPerQuery { get; set; }
    public long MaxDataTransferPerHour { get; set; } = 100_000_000; // 100MB
}
```

### Configuration Example

```json path=null start=null
{
  "PerformanceSettings": {
    "RateLimiting": {
      "EnableRateLimiting": true,
      "GlobalQueriesPerMinute": 1500,
      "GlobalConcurrentQueries": 150,
      "RoleBasedLimits": {
        "Admin": {
          "QueriesPerMinute": 150,
          "ConcurrentQueries": 15,
          "MaxRowsPerQuery": 500000,
          "MaxDataTransferPerHour": 500000000
        },
        "PowerUser": {
          "QueriesPerMinute": 60,
          "ConcurrentQueries": 8,
          "MaxRowsPerQuery": 50000,
          "MaxDataTransferPerHour": 200000000
        },
        "User": {
          "QueriesPerMinute": 30,
          "ConcurrentQueries": 5,
          "MaxRowsPerQuery": 20000,
          "MaxDataTransferPerHour": 100000000
        }
      },
      "CircuitBreakerFailureThreshold": 15,
      "CircuitBreakerRecoveryTime": "00:03:00"
    }
  }
}
```

## Resource Limits Configuration

### ResourceLimitSettings

Global resource consumption limits:

```csharp path=null start=null
public class ResourceLimitSettings
{
    // Memory limits
    public long MaxTotalMemoryUsage { get; set; } = 2_000_000_000; // 2GB
    public long MaxQueryMemoryUsage { get; set; } = 500_000_000; // 500MB per query
    public double MemoryPressureThreshold { get; set; } = 0.8; // 80%
    
    // CPU limits
    public double MaxCpuUsagePercent { get; set; } = 80.0;
    public TimeSpan CpuMonitoringInterval { get; set; } = TimeSpan.FromSeconds(30);
    
    // Storage limits
    public long MaxTempStorageUsage { get; set; } = 10_000_000_000; // 10GB
    public TimeSpan TempStorageCleanupInterval { get; set; } = TimeSpan.FromHours(1);
    
    // Connection limits
    public int MaxTotalConnections { get; set; } = 500;
    public int MaxConnectionsPerDataSource { get; set; } = 100;
    
    // Emergency thresholds
    public bool EnableEmergencyThrottling { get; set; } = true;
    public double EmergencyThrottleThreshold { get; set; } = 0.95; // 95% resource usage
    public TimeSpan EmergencyThrottleDuration { get; set; } = TimeSpan.FromMinutes(5);
}
```

## Monitoring Configuration

### MonitoringSettings

Performance monitoring and alerting:

```csharp path=null start=null
public class MonitoringSettings
{
    // Metrics collection
    public bool EnableMetrics { get; set; } = true;
    public TimeSpan MetricsCollectionInterval { get; set; } = TimeSpan.FromSeconds(30);
    public string MetricsConnectionString { get; set; } = "DefaultConnection";
    
    // Alerting
    public bool EnableAlerting { get; set; } = true;
    public List<string> AlertEmailRecipients { get; set; } = new();
    public Dictionary<string, AlertThreshold> AlertThresholds { get; set; } = new()
    {
        ["HighCpuUsage"] = new AlertThreshold { Threshold = 85.0, Duration = TimeSpan.FromMinutes(5) },
        ["HighMemoryUsage"] = new AlertThreshold { Threshold = 90.0, Duration = TimeSpan.FromMinutes(3) },
        ["LowCacheHitRatio"] = new AlertThreshold { Threshold = 60.0, Duration = TimeSpan.FromMinutes(10) },
        ["HighQueryFailureRate"] = new AlertThreshold { Threshold = 10.0, Duration = TimeSpan.FromMinutes(5) }
    };
    
    // Performance tracking
    public bool EnablePerformanceTracking { get; set; } = true;
    public TimeSpan PerformanceDataRetention { get; set; } = TimeSpan.FromDays(30);
    public int SlowQueryThresholdMs { get; set; } = 5000; // 5 seconds
}

public class AlertThreshold
{
    public double Threshold { get; set; }
    public TimeSpan Duration { get; set; }
    public string? NotificationChannel { get; set; }
}
```

## Environment-Specific Configuration

### Development Environment

```json path=null start=null
{
  "PerformanceSettings": {
    "ConnectionPool": {
      "DefaultPoolSize": 5,
      "MaxPoolSize": 20,
      "EnableWarmup": false
    },
    "QueryCache": {
      "DefaultExpiration": "00:05:00",
      "MaxCacheSize": 100000000,
      "RedisConnectionString": "localhost:6379"
    },
    "RateLimiting": {
      "EnableRateLimiting": false
    },
    "Monitoring": {
      "EnableAlerting": false,
      "MetricsCollectionInterval": "00:01:00"
    }
  }
}
```

### Production Environment

```json path=null start=null
{
  "PerformanceSettings": {
    "ConnectionPool": {
      "DefaultPoolSize": 50,
      "MaxPoolSize": 200,
      "EnableWarmup": true,
      "WarmupConnectionCount": 20
    },
    "QueryCache": {
      "DefaultExpiration": "00:30:00",
      "MaxCacheSize": 2000000000,
      "RedisConnectionString": "your-redis-cluster:6379",
      "EnableDistributedCache": true
    },
    "RateLimiting": {
      "EnableRateLimiting": true,
      "EnableCircuitBreaker": true
    },
    "Monitoring": {
      "EnableAlerting": true,
      "AlertEmailRecipients": ["admin@company.com", "ops@company.com"]
    }
  }
}
```

## Configuration Validation

The system includes configuration validation to ensure settings are within acceptable ranges:

```csharp path=null start=null
public class PerformanceSettingsValidator : IValidateOptions<PerformanceSettings>
{
    public ValidateOptionsResult Validate(string name, PerformanceSettings options)
    {
        var failures = new List<string>();
        
        // Validate connection pool settings
        if (options.ConnectionPool.MaxPoolSize < options.ConnectionPool.MinPoolSize)
            failures.Add("MaxPoolSize must be greater than MinPoolSize");
            
        if (options.ConnectionPool.DefaultPoolSize > options.ConnectionPool.MaxPoolSize)
            failures.Add("DefaultPoolSize cannot exceed MaxPoolSize");
            
        // Validate cache settings
        if (options.QueryCache.MaxExpiration < options.QueryCache.MinExpiration)
            failures.Add("MaxExpiration must be greater than MinExpiration");
            
        // Validate rate limiting
        if (options.RateLimiting.GlobalConcurrentQueries <= 0)
            failures.Add("GlobalConcurrentQueries must be positive");
            
        return failures.Any() 
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
```

## Related Documentation

- [Main Performance Architecture](./DataSources-Performance-Architecture.md)
- [Performance Interfaces Reference](./Performance-Interfaces.md)
- [Enterprise Service Implementation](./Enterprise-DataSource-Service.md)
- [Deployment & Monitoring Guide](./Performance-Deployment.md)