namespace ErpSystem.Core.Interfaces;

/// <summary>
/// Interface for performance analysis and monitoring
/// </summary>
public interface IPerformanceAnalyzer
{
    /// <summary>
    /// Log a slow database query for analysis
    /// </summary>
    Task LogSlowQuery(string query, TimeSpan duration, string source);

    /// <summary>
    /// Get performance baseline metrics
    /// </summary>
    Task<PerformanceMetrics> GetBaselineMetricsAsync();

    /// <summary>
    /// Analyze current system performance
    /// </summary>
    Task<PerformanceAnalysis> AnalyzePerformanceAsync();
}

/// <summary>
/// Performance metrics data
/// </summary>
public class PerformanceMetrics
{
    public double AverageQueryTime { get; set; }
    public int QueryCount { get; set; }
    public double MemoryUsageMB { get; set; }
    public double CpuUsagePercent { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Performance analysis results
/// </summary>
public class PerformanceAnalysis
{
    public List<string> SlowQueries { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
    public PerformanceMetrics CurrentMetrics { get; set; } = new();
    public PerformanceStatus Status { get; set; }
}

/// <summary>
/// Performance status levels
/// </summary>
public enum PerformanceStatus
{
    Excellent,
    Good,
    Fair,
    Poor,
    Critical
}
