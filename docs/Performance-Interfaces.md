# Performance Interfaces Reference

This document describes the interfaces that provide the enterprise-grade performance features for the Data Sources system.

## Overview

The performance architecture is built around five core interfaces:

1. **IConnectionPoolManager** - Connection pooling and management
2. **IQueryCacheService** - Query result caching with Redis
3. **IQueryExecutionService** - Background query processing
4. **IRateLimitingService** - Rate limiting and resource control
5. **Performance Configuration Classes** - Settings and configuration

## IConnectionPoolManager

Manages database connection pools to prevent connection exhaustion and improve performance.

### Purpose
- Maintain pools of reusable database connections
- Prevent connection exhaustion under high load
- Reduce connection establishment overhead
- Monitor connection health and performance

### Key Methods

```csharp
Task<IDbConnection> GetConnectionAsync(Guid dataSourceId, CancellationToken cancellationToken = default);
Task ReleaseConnectionAsync(Guid dataSourceId, IDbConnection connection);
Task WarmupPoolAsync(Guid dataSourceId, int connectionCount);
Task<ConnectionPoolStats> GetPoolStatsAsync(Guid dataSourceId);
```

### Features
- **Per-DataSource Pools**: Separate pools for each data source
- **Health Monitoring**: Automatic detection of stale connections
- **Pool Warmup**: Pre-populate pools for better initial performance
- **Statistics Tracking**: Monitor pool usage and efficiency

## IQueryCacheService

Provides intelligent caching of query results using Redis as the distributed cache backend.

### Purpose
- Cache frequently executed query results
- Reduce load on external data sources
- Improve response times for repeated queries
- Smart cache invalidation strategies

### Key Methods

```csharp
Task<QueryResultDto?> GetCachedResultAsync(string cacheKey, CancellationToken cancellationToken = default);
Task SetCachedResultAsync(string cacheKey, QueryResultDto result, TimeSpan? expiration = null);
Task InvalidateCacheAsync(Guid dataSourceId, CancellationToken cancellationToken = default);
string GenerateCacheKey(Guid dataSourceId, string query, Dictionary<string, object>? parameters = null);
```

### Features
- **Distributed Caching**: Redis-based caching for scalability
- **Smart Key Generation**: Consistent cache keys including parameters
- **Pattern-Based Invalidation**: Invalidate related cache entries
- **Configurable TTL**: Different expiration times for different query types
- **Compression**: Optional data compression to save memory

### Configuration Options

```csharp
public class QueryCacheConfiguration
{
    public bool EnableCaching { get; set; } = true;
    public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromMinutes(15);
    public long MaxCacheSize { get; set; } = 500_000_000; // 500MB
    public int MaxResultRows { get; set; } = 10_000;
    public bool CompressData { get; set; } = true;
}
```

## IQueryExecutionService

Handles long-running queries asynchronously through a background job queue system.

### Purpose
- Execute complex queries without blocking web requests
- Provide progress tracking for long operations
- Queue and prioritize query execution
- Store and retrieve query results

### Key Methods

```csharp
Task<string> SubmitQueryAsync(QueryExecutionRequest request, CancellationToken cancellationToken = default);
Task<QueryExecutionResult> GetQueryResultAsync(string executionId, CancellationToken cancellationToken = default);
Task<List<QueryExecutionStatus>> GetUserQueriesAsync(Guid userId, CancellationToken cancellationToken = default);
Task<bool> CancelQueryAsync(string executionId, Guid userId, CancellationToken cancellationToken = default);
```

### Features
- **Asynchronous Processing**: Non-blocking query execution
- **Progress Tracking**: Real-time progress updates
- **Priority Queues**: High-priority queries get processed first
- **Result Storage**: Temporary storage for query results
- **User Query Management**: Track queries per user

### Query States

- **Submitted** - Query request received
- **Queued** - Waiting in the execution queue
- **Running** - Currently being executed
- **Completed** - Successfully finished
- **Failed** - Execution failed with error
- **Cancelled** - User or system cancelled
- **Timeout** - Query exceeded time limit

## IRateLimitingService

Implements rate limiting and resource control to ensure fair usage and prevent system overload.

### Purpose
- Prevent individual users from overwhelming the system
- Implement role-based resource quotas
- Track and limit concurrent operations
- Circuit breaker functionality for failing data sources

### Key Methods

```csharp
Task<RateLimitResult> CheckRateLimitAsync(RateLimitRequest request, CancellationToken cancellationToken = default);
Task<RateLimitStatus> GetUserLimitsAsync(Guid userId, CancellationToken cancellationToken = default);
Task<Dictionary<string, RateLimitStats>> GetSystemLimitsAsync(CancellationToken cancellationToken = default);
Task ResetUserLimitsAsync(Guid userId, RateLimitType limitType);
```

### Rate Limit Types

- **QueryExecution** - Limits on query execution frequency
- **ConcurrentQueries** - Maximum simultaneous queries per user
- **DataTransfer** - Bandwidth/data volume limits
- **ApiRequests** - General API request rate limiting

### Features
- **Sliding Window**: More accurate than fixed-window limiting
- **Role-Based Limits**: Different quotas for different user roles
- **Data Source Limits**: Per-datasource rate limiting
- **Circuit Breakers**: Automatic failover for problematic data sources

## Performance Monitoring

All interfaces include comprehensive monitoring and metrics collection:

### Connection Pool Metrics
- Active connections per pool
- Connection wait times
- Pool hit/miss ratios
- Connection lifecycle events

### Cache Metrics
- Cache hit/miss ratios
- Memory usage statistics
- Average response times
- Cache invalidation events

### Query Execution Metrics
- Queue lengths and wait times
- Execution time distributions
- Success/failure rates
- Resource utilization

### Rate Limiting Metrics
- Rate limit violations per user/datasource
- Current usage vs. limits
- Circuit breaker state changes

## Integration Example

```csharp
public class EnterpriseDataSourceService : IDataSourceService
{
    private readonly IConnectionPoolManager _connectionPool;
    private readonly IQueryCacheService _cache;
    private readonly IQueryExecutionService _queryExecutor;
    private readonly IRateLimitingService _rateLimiter;

    public async Task<QueryResultDto> ExecuteQueryAsync(
        Guid dataSourceId, 
        QueryDataSourceDto queryDto, 
        Guid tenantId)
    {
        // 1. Check rate limits
        var rateLimitResult = await _rateLimiter.CheckRateLimitAsync(request);
        if (!rateLimitResult.IsAllowed)
            throw new RateLimitExceededException(rateLimitResult.ReasonMessage);

        // 2. Check cache
        var cacheKey = _cache.GenerateCacheKey(dataSourceId, queryDto.Query);
        var cached = await _cache.GetCachedResultAsync(cacheKey);
        if (cached != null) return cached;

        // 3. Execute query (direct or background)
        QueryResultDto result;
        if (ShouldUseBackgroundProcessing(queryDto))
        {
            var executionId = await _queryExecutor.SubmitQueryAsync(request);
            result = await _queryExecutor.GetQueryResultAsync(executionId);
        }
        else
        {
            using var connection = await _connectionPool.GetConnectionAsync(dataSourceId);
            result = await ExecuteDirectly(connection, queryDto);
        }

        // 4. Cache result
        await _cache.SetCachedResultAsync(cacheKey, result, expiration);
        
        return result;
    }
}
```

## Next Steps

1. **Implementation**: Implement concrete classes for each interface
2. **Configuration**: Set up Redis and job queue infrastructure  
3. **Testing**: Unit and integration tests for all components
4. **Monitoring**: Set up dashboards and alerts
5. **Documentation**: API documentation and deployment guides

## Related Documentation

- [Main Performance Architecture](./DataSources-Performance-Architecture.md)
- [Configuration Guide](./Performance-Configuration.md)
- [Enterprise Service Implementation](./Enterprise-DataSource-Service.md)
- [Deployment & Monitoring](./Performance-Deployment.md)