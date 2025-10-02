using ErpSystem.Core.DTOs.DataSources;

namespace ErpSystem.Core.Services
{
    public interface IQueryExecutionService
    {
        Task<string> SubmitQueryAsync(QueryExecutionRequest request, CancellationToken cancellationToken = default);
        Task<QueryExecutionResult> GetQueryResultAsync(string executionId, CancellationToken cancellationToken = default);
        Task<List<QueryExecutionStatus>> GetUserQueriesAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<bool> CancelQueryAsync(string executionId, Guid userId, CancellationToken cancellationToken = default);
        Task<QueryPerformanceMetrics> GetPerformanceMetricsAsync(CancellationToken cancellationToken = default);
    }

    public class QueryExecutionRequest
    {
        public Guid DataSourceId { get; set; }
        public string Query { get; set; } = string.Empty;
        public Dictionary<string, object>? Parameters { get; set; }
        public int MaxRows { get; set; } = 10_000;
        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
        public QueryPriority Priority { get; set; } = QueryPriority.Normal;
        public Guid UserId { get; set; }
        public Guid TenantId { get; set; }
        public bool CacheResult { get; set; } = true;
        public TimeSpan? CacheExpiration { get; set; }
    }

    public class QueryExecutionResult
    {
        public string ExecutionId { get; set; } = string.Empty;
        public QueryExecutionState State { get; set; }
        public QueryResultDto? Result { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime SubmittedAt { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public TimeSpan? ExecutionTime { get; set; }
        public QueryPriority Priority { get; set; }
        public double Progress { get; set; }
        public string? StatusMessage { get; set; }
    }

    public class QueryExecutionStatus
    {
        public string ExecutionId { get; set; } = string.Empty;
        public string QueryPreview { get; set; } = string.Empty;
        public QueryExecutionState State { get; set; }
        public DateTime SubmittedAt { get; set; }
        public TimeSpan? ExecutionTime { get; set; }
        public double Progress { get; set; }
        public int? RowCount { get; set; }
    }

    public class QueryPerformanceMetrics
    {
        public int TotalQueries { get; set; }
        public int QueriesInProgress { get; set; }
        public int QueriesInQueue { get; set; }
        public TimeSpan AverageExecutionTime { get; set; }
        public TimeSpan AverageQueueTime { get; set; }
        public Dictionary<string, int> QueriesByDataSource { get; set; } = new();
        public Dictionary<QueryPriority, int> QueriesByPriority { get; set; } = new();
        public double ThroughputPerMinute { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public enum QueryExecutionState
    {
        Submitted,
        Queued,
        Running,
        Completed,
        Failed,
        Cancelled,
        Timeout
    }

    public enum QueryPriority
    {
        Low = 1,
        Normal = 2,
        High = 3,
        Critical = 4
    }
}