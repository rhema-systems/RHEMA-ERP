using System.Data;

namespace ErpSystem.Core.Services
{
    public interface IConnectionPoolManager
    {
        Task<IDbConnection> GetConnectionAsync(Guid dataSourceId, CancellationToken cancellationToken = default);
        Task ReleaseConnectionAsync(Guid dataSourceId, IDbConnection connection);
        Task<ConnectionPoolStats> GetPoolStatsAsync(Guid dataSourceId);
        Task ClearPoolAsync(Guid dataSourceId);
        Task WarmupPoolAsync(Guid dataSourceId, int connectionCount = 5);
    }

    public class ConnectionPoolStats
    {
        public int TotalConnections { get; set; }
        public int ActiveConnections { get; set; }
        public int IdleConnections { get; set; }
        public int WaitingRequests { get; set; }
        public TimeSpan AverageWaitTime { get; set; }
        public DateTime LastUsed { get; set; }
    }

    public class ConnectionPoolConfiguration
    {
        public int MinPoolSize { get; set; } = 2;
        public int MaxPoolSize { get; set; } = 20;
        public TimeSpan ConnectionLifetime { get; set; } = TimeSpan.FromMinutes(30);
        public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(10);
        public TimeSpan AcquisitionTimeout { get; set; } = TimeSpan.FromSeconds(30);
        public bool EnablePooling { get; set; } = true;
    }
}