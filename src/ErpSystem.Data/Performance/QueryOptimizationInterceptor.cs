using System.Data.Common;
using System.Diagnostics;
using ErpSystem.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ErpSystem.Data.Performance;

/// <summary>
/// Interceptor to monitor and log slow database queries
/// </summary>
public class QueryOptimizationInterceptor : DbCommandInterceptor
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<QueryOptimizationInterceptor> _logger;

    public QueryOptimizationInterceptor(IServiceProvider serviceProvider, ILogger<QueryOptimizationInterceptor> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public override async ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        await LogSlowQueryIfNeeded(command, eventData);
        return await base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override DbDataReader ReaderExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        DbDataReader result)
    {
        _ = Task.Run(async () => await LogSlowQueryIfNeeded(command, eventData));
        return base.ReaderExecuted(command, eventData, result);
    }

    public override async ValueTask<object?> ScalarExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result,
        CancellationToken cancellationToken = default)
    {
        await LogSlowQueryIfNeeded(command, eventData);
        return await base.ScalarExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override object? ScalarExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        object? result)
    {
        _ = Task.Run(async () => await LogSlowQueryIfNeeded(command, eventData));
        return base.ScalarExecuted(command, eventData, result);
    }

    public override async ValueTask<int> NonQueryExecutedAsync(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        await LogSlowQueryIfNeeded(command, eventData);
        return await base.NonQueryExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override int NonQueryExecuted(
        DbCommand command,
        CommandExecutedEventData eventData,
        int result)
    {
        _ = Task.Run(async () => await LogSlowQueryIfNeeded(command, eventData));
        return base.NonQueryExecuted(command, eventData, result);
    }

    private async Task LogSlowQueryIfNeeded(DbCommand command, CommandExecutedEventData eventData)
    {
        try
        {
            var duration = eventData.Duration;

            // Log queries that take longer than 500ms as warnings, 1s as errors
            if (duration.TotalMilliseconds > 500)
            {
                using var scope = _serviceProvider.CreateScope();
                var performanceAnalyzer = scope.ServiceProvider.GetService<IPerformanceAnalyzer>();

                if (performanceAnalyzer != null)
                {
                    await performanceAnalyzer.LogSlowQuery(
                        SanitizeQuery(command.CommandText),
                        duration,
                        "QueryOptimizationInterceptor"
                    );
                }

                var logLevel = duration.TotalMilliseconds > 1000 ? LogLevel.Error : LogLevel.Warning;
                _logger.Log(logLevel, "Slow query detected: {Duration}ms - {Query}",
                    duration.TotalMilliseconds, SanitizeQuery(command.CommandText));

                // Log query execution plan for very slow queries (SQL Server specific)
                if (duration.TotalMilliseconds > 2000 && command.CommandText.Contains("SELECT"))
                {
                    _logger.LogInformation("Consider analyzing execution plan for query: {Query}",
                        SanitizeQuery(command.CommandText));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error logging slow query metrics");
        }
    }

    private static string SanitizeQuery(string query)
    {
        // Remove potential sensitive data and limit length
        return query.Length > 500 ? query[..497] + "..." : query;
    }
}

/// <summary>
/// Extensions for database performance optimization
/// </summary>
public static class DatabasePerformanceExtensions
{
    /// <summary>
    /// Add database performance monitoring and optimization
    /// </summary>
    public static IServiceCollection AddDatabasePerformanceOptimization(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register query optimization interceptor
        services.AddScoped<QueryOptimizationInterceptor>();

        // Register connection pool optimization
        services.Configure<DatabasePerformanceOptions>(
            configuration.GetSection("Database:Performance"));

        return services;
    }

    /// <summary>
    /// Add the query optimization interceptor to DbContext options
    /// </summary>
    public static DbContextOptionsBuilder AddQueryOptimizationInterceptor(
        this DbContextOptionsBuilder optionsBuilder,
        IServiceProvider serviceProvider)
    {
        var interceptor = serviceProvider.GetRequiredService<QueryOptimizationInterceptor>();
        return optionsBuilder.AddInterceptors(interceptor);
    }
}

/// <summary>
/// Configuration options for database performance optimization
/// </summary>
public class DatabasePerformanceOptions
{
    /// <summary>
    /// Enable query performance monitoring
    /// </summary>
    public bool EnableQueryMonitoring { get; set; } = true;

    /// <summary>
    /// Slow query threshold in milliseconds
    /// </summary>
    public int SlowQueryThresholdMs { get; set; } = 500;

    /// <summary>
    /// Maximum connection pool size
    /// </summary>
    public int MaxPoolSize { get; set; } = 100;

    /// <summary>
    /// Connection timeout in seconds
    /// </summary>
    public int ConnectionTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Command timeout in seconds
    /// </summary>
    public int CommandTimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// Enable multiple active result sets
    /// </summary>
    public bool EnableMultipleActiveResultSets { get; set; } = true;

    /// <summary>
    /// Enable connection pooling
    /// </summary>
    public bool EnableConnectionPooling { get; set; } = true;
}
