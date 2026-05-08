using System.Diagnostics;
using ErpSystem.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ErpSystem.Api.Performance;

/// <summary>
/// Performance analyzer and monitoring service
/// </summary>
public interface IPerformanceAnalyzer
{
    Task<PerformanceMetrics> GetCurrentMetricsAsync();
    Task<DatabasePerformanceMetrics> AnalyzeDatabasePerformanceAsync();
    Task<ApiPerformanceMetrics> AnalyzeApiPerformanceAsync();
    Task LogSlowQuery(string query, TimeSpan duration, string source);
}

public class PerformanceAnalyzer : IPerformanceAnalyzer
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PerformanceAnalyzer> _logger;
    private readonly IServiceProvider _serviceProvider;

    public PerformanceAnalyzer(
        ApplicationDbContext context,
        ILogger<PerformanceAnalyzer> logger,
        IServiceProvider serviceProvider)
    {
        _context = context;
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    public Task<PerformanceMetrics> GetCurrentMetricsAsync()
    {
        var process = Process.GetCurrentProcess();

        return Task.FromResult(new PerformanceMetrics
        {
            Timestamp = DateTime.UtcNow,
            MemoryUsage = new MemoryMetrics
            {
                WorkingSet = process.WorkingSet64,
                PrivateMemorySize = process.PrivateMemorySize64,
                VirtualMemorySize = process.VirtualMemorySize64,
                GCMemory = GC.GetTotalMemory(false)
            },
            ProcessorTime = process.TotalProcessorTime,
            ThreadCount = process.Threads.Count,
            HandleCount = process.HandleCount
        });
    }

    public async Task<DatabasePerformanceMetrics> AnalyzeDatabasePerformanceAsync()
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Test basic connection
            var connectionTime = await MeasureConnectionTimeAsync();

            // Analyze query performance on key tables
            var auditLogCount = await _context.AuditLogs.CountAsync();
            var userCount = await _context.Users.CountAsync();
            var tenantCount = await _context.Tenants.CountAsync();

            stopwatch.Stop();

            return new DatabasePerformanceMetrics
            {
                ConnectionTime = connectionTime,
                AnalysisTime = stopwatch.Elapsed,
                TableMetrics = new Dictionary<string, TableMetrics>
                {
                    ["AuditLogs"] = new() { RowCount = auditLogCount, EstimatedSizeMB = auditLogCount * 0.005 }, // ~5KB per audit log
                    ["Users"] = new() { RowCount = userCount, EstimatedSizeMB = userCount * 0.002 }, // ~2KB per user
                    ["Tenants"] = new() { RowCount = tenantCount, EstimatedSizeMB = tenantCount * 0.001 } // ~1KB per tenant
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing database performance");
            throw;
        }
    }

    public Task<ApiPerformanceMetrics> AnalyzeApiPerformanceAsync()
    {
        // This would typically analyze API response times from collected metrics
        // For now, we'll return a basic structure
        return Task.FromResult(new ApiPerformanceMetrics
        {
            AverageResponseTime = TimeSpan.FromMilliseconds(150), // Placeholder
            P95ResponseTime = TimeSpan.FromMilliseconds(500),     // Placeholder
            RequestsPerSecond = 25.5,                            // Placeholder
            ErrorRate = 0.02                                     // 2% error rate placeholder
        });
    }

    public Task LogSlowQuery(string query, TimeSpan duration, string source)
    {
        if (duration.TotalMilliseconds > 1000) // Log queries slower than 1 second
        {
            _logger.LogWarning("Slow query detected: {Duration}ms in {Source}. Query: {Query}",
                duration.TotalMilliseconds, source, query);
        }

        return Task.CompletedTask;
    }

    private async Task<TimeSpan> MeasureConnectionTimeAsync()
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _context.Database.ExecuteSqlRawAsync("SELECT 1");
            return stopwatch.Elapsed;
        }
        catch
        {
            return TimeSpan.FromMilliseconds(-1); // Indicate connection failure
        }
        finally
        {
            stopwatch.Stop();
        }
    }
}

// Performance metric models
public class PerformanceMetrics
{
    public DateTime Timestamp { get; set; }
    public MemoryMetrics MemoryUsage { get; set; } = new();
    public TimeSpan ProcessorTime { get; set; }
    public int ThreadCount { get; set; }
    public int HandleCount { get; set; }
}

public class MemoryMetrics
{
    public long WorkingSet { get; set; }
    public long PrivateMemorySize { get; set; }
    public long VirtualMemorySize { get; set; }
    public long GCMemory { get; set; }

    public double WorkingSetMB => WorkingSet / (1024.0 * 1024.0);
    public double PrivateMemoryMB => PrivateMemorySize / (1024.0 * 1024.0);
    public double GCMemoryMB => GCMemory / (1024.0 * 1024.0);
}

public class DatabasePerformanceMetrics
{
    public TimeSpan ConnectionTime { get; set; }
    public TimeSpan AnalysisTime { get; set; }
    public Dictionary<string, TableMetrics> TableMetrics { get; set; } = new();
}

public class TableMetrics
{
    public int RowCount { get; set; }
    public double EstimatedSizeMB { get; set; }
}

public class ApiPerformanceMetrics
{
    public TimeSpan AverageResponseTime { get; set; }
    public TimeSpan P95ResponseTime { get; set; }
    public double RequestsPerSecond { get; set; }
    public double ErrorRate { get; set; }
}
