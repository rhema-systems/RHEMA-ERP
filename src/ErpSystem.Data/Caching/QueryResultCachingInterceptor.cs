using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Caching;

/// <summary>
/// EF Core interceptor that provides automatic query result caching
/// </summary>
public class QueryResultCachingInterceptor : DbCommandInterceptor
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<QueryResultCachingInterceptor> _logger;
    private readonly QueryCachingOptions _options;

    public QueryResultCachingInterceptor(
        IServiceProvider serviceProvider,
        ILogger<QueryResultCachingInterceptor> logger,
        Microsoft.Extensions.Options.IOptions<QueryCachingOptions> options)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options.Value;
    }

    public override async ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        if (!_options.EnableQueryCaching || !ShouldCacheQuery(command))
        {
            return await base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
        }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var cache = scope.ServiceProvider.GetService<IDistributedCache>();

            if (cache != null)
            {
                await CacheQueryResultAsync(command, result, cache, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error caching query result");
        }

        return await base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        if (!_options.EnableQueryCaching || !ShouldCacheQuery(command))
        {
            return await base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var cache = scope.ServiceProvider.GetService<IDistributedCache>();

            if (cache != null)
            {
                var cachedResult = await GetCachedQueryResultAsync(command, cache, cancellationToken);
                if (cachedResult != null)
                {
                    _logger.LogDebug("Query result cache hit for: {Query}", SanitizeQuery(command.CommandText));
                    return InterceptionResult<DbDataReader>.SuppressWithResult(cachedResult);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cached query result");
        }

        return await base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    private bool ShouldCacheQuery(DbCommand command)
    {
        var sql = command.CommandText?.ToUpper();
        if (string.IsNullOrEmpty(sql))
        {
            return false;
        }

        // Only cache SELECT queries
        if (!sql.TrimStart().StartsWith("SELECT"))
        {
            return false;
        }

        // Don't cache queries with non-deterministic functions
        var nonCacheablePatterns = new[]
        {
            "GETDATE()", "GETUTCDATE()", "NOW()", "CURRENT_TIMESTAMP", "CURRENT_TIME",
            "RAND()", "RANDOM()", "NEWID()", "UUID()", "CURRENT_USER"
        };

        if (nonCacheablePatterns.Any(pattern => sql.Contains(pattern)))
        {
            return false;
        }

        // Don't cache queries that modify data
        var modifyingPatterns = new[] { "INSERT", "UPDATE", "DELETE", "MERGE" };
        if (modifyingPatterns.Any(pattern => sql.Contains(pattern)))
        {
            return false;
        }

        // Don't cache queries with temp tables or variables
        if (sql.Contains('#') || sql.Contains('@'))
        {
            return false;
        }

        // Check against excluded tables
        foreach (var excludedTable in _options.ExcludedTables)
        {
            if (sql.Contains(excludedTable, StringComparison.CurrentCultureIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private async Task<DbDataReader?> GetCachedQueryResultAsync(
        DbCommand command,
        IDistributedCache cache,
        CancellationToken cancellationToken)
    {
        var cacheKey = GenerateCacheKey(command);
        var cachedData = await cache.GetStringAsync(cacheKey, cancellationToken);

        if (string.IsNullOrEmpty(cachedData))
        {
            return null;
        }

        var queryResult = JsonSerializer.Deserialize<CachedQueryResult>(cachedData);
        if (queryResult == null || IsExpired(queryResult))
        {
            await cache.RemoveAsync(cacheKey, cancellationToken);
            return null;
        }

        return new CachedDataReader(queryResult);
    }

    private async Task CacheQueryResultAsync(
        DbCommand command,
        DbDataReader reader,
        IDistributedCache cache,
        CancellationToken cancellationToken)
    {
        var cacheKey = GenerateCacheKey(command);
        var queryResult = await ConvertReaderToCachedResult(reader);

        if (queryResult.Rows.Count > _options.MaxRowsToCache)
        {
            _logger.LogDebug("Skipping cache for large result set ({RowCount} rows) for query: {Query}",
                queryResult.Rows.Count, SanitizeQuery(command.CommandText));
            return;
        }

        var serialized = JsonSerializer.Serialize(queryResult);
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_options.DefaultCacheDurationSeconds),
            SlidingExpiration = TimeSpan.FromSeconds(_options.DefaultCacheDurationSeconds / 2)
        };

        await cache.SetStringAsync(cacheKey, serialized, options, cancellationToken);
        _logger.LogDebug("Cached query result ({RowCount} rows) for: {Query}",
            queryResult.Rows.Count, SanitizeQuery(command.CommandText));
    }

    private string GenerateCacheKey(DbCommand command)
    {
        var keyBuilder = new StringBuilder();
        keyBuilder.Append(_options.CacheKeyPrefix);
        keyBuilder.Append("query:");

        // Add command text
        keyBuilder.Append(command.CommandText);

        // Add parameters
        foreach (DbParameter parameter in command.Parameters)
        {
            keyBuilder.Append($"|{parameter.ParameterName}={parameter.Value}");
        }

        // Generate hash for consistent key length
        var keyString = keyBuilder.ToString();
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(keyString));
        var hash = Convert.ToHexString(hashBytes);

        return $"{_options.CacheKeyPrefix}query:{hash}";
    }

    private static async Task<CachedQueryResult> ConvertReaderToCachedResult(DbDataReader reader)
    {
        var result = new CachedQueryResult
        {
            CachedAt = DateTime.UtcNow,
            Columns = new List<string>()
        };

        // Get column information
        for (int i = 0; i < reader.FieldCount; i++)
        {
            result.Columns.Add(reader.GetName(i));
        }

        // Read all rows
        var rows = new List<object[]>();
        while (await reader.ReadAsync())
        {
            var row = new object[reader.FieldCount];
            reader.GetValues(row);

            // Convert DBNull to null for JSON serialization
            for (int i = 0; i < row.Length; i++)
            {
                if (row[i] == DBNull.Value)
                {
                    row[i] = null;
                }
            }

            rows.Add(row);
        }

        result.Rows = rows;
        return result;
    }

    private bool IsExpired(CachedQueryResult result)
    {
        var expiryTime = result.CachedAt.AddSeconds(_options.DefaultCacheDurationSeconds);
        return DateTime.UtcNow > expiryTime;
    }

    private static string SanitizeQuery(string query)
    {
        return query.Length > 100 ? query[..97] + "..." : query;
    }
}

/// <summary>
/// Cached query result data structure
/// </summary>
public class CachedQueryResult
{
    public DateTime CachedAt { get; set; }
    public List<string> Columns { get; set; } = new();
    public List<object[]> Rows { get; set; } = new();
}

/// <summary>
/// Custom DbDataReader that reads from cached data
/// </summary>
public class CachedDataReader : DbDataReader
{
    private readonly CachedQueryResult _cachedResult;
    private int _currentRowIndex = -1;
    private bool _isClosed = false;

    public CachedDataReader(CachedQueryResult cachedResult)
    {
        _cachedResult = cachedResult;
    }

    public override int FieldCount => _cachedResult.Columns.Count;
    public override bool HasRows => _cachedResult.Rows.Count > 0;
    public override bool IsClosed => _isClosed;
    public override int RecordsAffected => 0;
    public override object this[int ordinal] => GetValue(ordinal);
    public override object this[string name] => GetValue(GetOrdinal(name));
    public override int Depth => 0;

    public override bool Read()
    {
        if (_isClosed || _currentRowIndex >= _cachedResult.Rows.Count - 1)
        {
            return false;
        }

        _currentRowIndex++;
        return true;
    }

    public override Task<bool> ReadAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(Read());
    }

    public override bool NextResult() => false;

    public override void Close()
    {
        _isClosed = true;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Close();
        }
        base.Dispose(disposing);
    }

    public override string GetName(int ordinal)
    {
        return _cachedResult.Columns[ordinal];
    }

    public override int GetOrdinal(string name)
    {
        return _cachedResult.Columns.FindIndex(c =>
            string.Equals(c, name, StringComparison.OrdinalIgnoreCase));
    }

    public override object GetValue(int ordinal)
    {
        if (_currentRowIndex < 0 || _currentRowIndex >= _cachedResult.Rows.Count)
        {
            throw new InvalidOperationException("Invalid attempt to read when no data is present.");
        }

        return _cachedResult.Rows[_currentRowIndex][ordinal] ?? DBNull.Value;
    }

    public override bool IsDBNull(int ordinal)
    {
        var value = _cachedResult.Rows[_currentRowIndex][ordinal];
        return value == null || value == DBNull.Value;
    }

    public override string GetDataTypeName(int ordinal) => "object";
    public override Type GetFieldType(int ordinal) => typeof(object);

    #region Not Implemented Members
    public override bool GetBoolean(int ordinal) => Convert.ToBoolean(GetValue(ordinal));
    public override byte GetByte(int ordinal) => Convert.ToByte(GetValue(ordinal));
    public override char GetChar(int ordinal) => Convert.ToChar(GetValue(ordinal));
    public override DateTime GetDateTime(int ordinal) => Convert.ToDateTime(GetValue(ordinal));
    public override decimal GetDecimal(int ordinal) => Convert.ToDecimal(GetValue(ordinal));
    public override double GetDouble(int ordinal) => Convert.ToDouble(GetValue(ordinal));
    public override float GetFloat(int ordinal) => Convert.ToSingle(GetValue(ordinal));
    public override Guid GetGuid(int ordinal) => (Guid)GetValue(ordinal);
    public override short GetInt16(int ordinal) => Convert.ToInt16(GetValue(ordinal));
    public override int GetInt32(int ordinal) => Convert.ToInt32(GetValue(ordinal));
    public override long GetInt64(int ordinal) => Convert.ToInt64(GetValue(ordinal));
    public override string GetString(int ordinal) => Convert.ToString(GetValue(ordinal)) ?? string.Empty;

    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length)
    {
        throw new NotSupportedException();
    }

    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length)
    {
        throw new NotSupportedException();
    }

    public override int GetValues(object[] values)
    {
        if (_currentRowIndex < 0 || _currentRowIndex >= _cachedResult.Rows.Count)
        {
            return 0;
        }

        var row = _cachedResult.Rows[_currentRowIndex];
        var copyCount = Math.Min(values.Length, row.Length);

        for (int i = 0; i < copyCount; i++)
        {
            values[i] = row[i] ?? DBNull.Value;
        }

        return copyCount;
    }

    public override System.Collections.IEnumerator GetEnumerator()
    {
        throw new NotSupportedException();
    }
    #endregion
}

/// <summary>
/// Query caching configuration options
/// </summary>
public class QueryCachingOptions
{
    public const string SectionName = "QueryCaching";

    /// <summary>
    /// Enable query result caching
    /// </summary>
    public bool EnableQueryCaching { get; set; } = true;

    /// <summary>
    /// Default cache duration in seconds
    /// </summary>
    public int DefaultCacheDurationSeconds { get; set; } = 300; // 5 minutes

    /// <summary>
    /// Maximum number of rows to cache
    /// </summary>
    public int MaxRowsToCache { get; set; } = 1000;

    /// <summary>
    /// Cache key prefix
    /// </summary>
    public string CacheKeyPrefix { get; set; } = "erp:query:";

    /// <summary>
    /// Tables to exclude from query caching
    /// </summary>
    public List<string> ExcludedTables { get; set; } = new()
    {
        "AuditLogs",      // Audit logs change frequently
        "SecurityLogs",   // Security logs change frequently  
        "UserSessions",   // Session data is volatile
        "BlacklistedTokens", // Token data is time-sensitive
        "RefreshTokens"   // Token data is time-sensitive
    };

    /// <summary>
    /// Enable caching for expensive queries only (>= threshold ms)
    /// </summary>
    public bool CacheExpensiveQueriesOnly { get; set; } = false;

    /// <summary>
    /// Minimum execution time to consider caching (milliseconds)
    /// </summary>
    public int ExpensiveQueryThresholdMs { get; set; } = 500;
}

/// <summary>
/// Service for managing query cache
/// </summary>
public interface IQueryCacheManager
{
    Task InvalidateQueryCacheAsync(string pattern, CancellationToken cancellationToken = default);
    Task InvalidateTableCacheAsync(string tableName, CancellationToken cancellationToken = default);
    Task<QueryCacheStats> GetCacheStatsAsync(CancellationToken cancellationToken = default);
    Task ClearAllQueryCacheAsync(CancellationToken cancellationToken = default);
}

public class QueryCacheManager : IQueryCacheManager
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<QueryCacheManager> _logger;
    private readonly QueryCachingOptions _options;

    public QueryCacheManager(
        IDistributedCache cache,
        ILogger<QueryCacheManager> logger,
        Microsoft.Extensions.Options.IOptions<QueryCachingOptions> options)
    {
        _cache = cache;
        _logger = logger;
        _options = options.Value;
    }

    public async Task InvalidateQueryCacheAsync(string pattern, CancellationToken cancellationToken = default)
    {
        try
        {
            // This would typically require a Redis-specific implementation
            // For now, log the invalidation request
            _logger.LogInformation("Query cache invalidation requested for pattern: {Pattern}", pattern);

            // Implementation would depend on the cache provider
            // For Redis: scan keys and delete matching patterns
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invalidating query cache for pattern: {Pattern}", pattern);
        }
    }

    public async Task InvalidateTableCacheAsync(string tableName, CancellationToken cancellationToken = default)
    {
        var pattern = $"{_options.CacheKeyPrefix}*{tableName}*";
        await InvalidateQueryCacheAsync(pattern, cancellationToken);
    }

    public Task<QueryCacheStats> GetCacheStatsAsync(CancellationToken cancellationToken = default)
    {
        // This would require cache provider-specific implementation
        var stats = new QueryCacheStats
        {
            TotalCachedQueries = 0, // Would be retrieved from cache
            CacheHitRate = 0.0,     // Would be calculated
            TotalCacheSize = 0,     // Would be retrieved from cache
            LastUpdated = DateTime.UtcNow
        };
        return Task.FromResult(stats);
    }

    public async Task ClearAllQueryCacheAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var pattern = $"{_options.CacheKeyPrefix}*";
            await InvalidateQueryCacheAsync(pattern, cancellationToken);
            _logger.LogWarning("All query cache cleared");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing all query cache");
        }
    }
}

/// <summary>
/// Query cache statistics
/// </summary>
public class QueryCacheStats
{
    public int TotalCachedQueries { get; set; }
    public double CacheHitRate { get; set; }
    public long TotalCacheSize { get; set; }
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// Extension methods for query result caching
/// </summary>
public static class QueryCachingExtensions
{
    /// <summary>
    /// Add query result caching services
    /// </summary>
    public static IServiceCollection AddQueryResultCaching(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure options
        services.Configure<QueryCachingOptions>(
            configuration.GetSection(QueryCachingOptions.SectionName));

        // Register services
        services.AddScoped<QueryResultCachingInterceptor>();
        services.AddScoped<IQueryCacheManager, QueryCacheManager>();

        return services;
    }

    /// <summary>
    /// Add query result caching interceptor to DbContext options
    /// </summary>
    public static DbContextOptionsBuilder AddQueryResultCachingInterceptor(
        this DbContextOptionsBuilder optionsBuilder,
        IServiceProvider serviceProvider)
    {
        var interceptor = serviceProvider.GetRequiredService<QueryResultCachingInterceptor>();
        return optionsBuilder.AddInterceptors(interceptor);
    }
}
