using ErpSystem.Core.DTOs.DataSources;

namespace ErpSystem.Core.Services
{
    public interface IQueryCacheService
    {
        Task<QueryResultDto?> GetCachedResultAsync(string cacheKey, CancellationToken cancellationToken = default);
        Task SetCachedResultAsync(string cacheKey, QueryResultDto result, TimeSpan? expiration = null, CancellationToken cancellationToken = default);
        Task InvalidateCacheAsync(Guid dataSourceId, CancellationToken cancellationToken = default);
        Task InvalidateCachePatternAsync(string pattern, CancellationToken cancellationToken = default);
        string GenerateCacheKey(Guid dataSourceId, string query, Dictionary<string, object>? parameters = null);
        Task<CacheStats> GetCacheStatsAsync(CancellationToken cancellationToken = default);
    }

    public class CacheStats
    {
        public long TotalKeys { get; set; }
        public long HitCount { get; set; }
        public long MissCount { get; set; }
        public double HitRatio => TotalRequests > 0 ? (double)HitCount / TotalRequests : 0;
        public long TotalRequests => HitCount + MissCount;
        public long MemoryUsageBytes { get; set; }
        public TimeSpan AverageResponseTime { get; set; }
    }

    public class QueryCacheConfiguration
    {
        public bool EnableCaching { get; set; } = true;
        public TimeSpan DefaultExpiration { get; set; } = TimeSpan.FromMinutes(15);
        public TimeSpan MaxExpiration { get; set; } = TimeSpan.FromHours(4);
        public long MaxCacheSize { get; set; } = 500_000_000; // 500MB
        public int MaxResultRows { get; set; } = 10_000; // Don't cache results larger than 10k rows
        public bool CompressData { get; set; } = true;
    }
}