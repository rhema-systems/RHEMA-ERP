using ErpSystem.Core.Entities;

namespace ErpSystem.Core.Interfaces;

public interface IDataSourceRepository : IGenericRepository<DataSource>
{
    Task<IEnumerable<DataSource>> GetDataSourcesByTenantAsync(Guid tenantId, bool activeOnly = false);
    Task<DataSource?> GetDataSourceWithUsageAsync(Guid dataSourceId, Guid tenantId);
    Task<bool> IsDataSourceNameExistsAsync(string name, Guid tenantId, Guid? excludeId = null);
    Task UpdateUsageCountAsync(Guid dataSourceId);
    Task UpdateConnectionStatusAsync(Guid dataSourceId, bool isSuccess, string? errorMessage = null);
    Task<IEnumerable<DataSource>> GetRecentlyUsedAsync(Guid tenantId, int count = 10);
    Task<DataSource?> GetByNameAsync(string name, Guid tenantId);
}