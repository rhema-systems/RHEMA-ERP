using ErpSystem.Core.Entities;
using ErpSystem.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories;

public class DataSourceRepository : GenericRepository<DataSource>, IDataSourceRepository
{
    public DataSourceRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<DataSource>> GetDataSourcesByTenantAsync(Guid tenantId, bool activeOnly = false)
    {
        var query = _context.Set<DataSource>()
            .Where(ds => ds.TenantId == tenantId && !ds.IsDeleted);

        if (activeOnly)
        {
            query = query.Where(ds => ds.IsActive);
        }

        return await query
            .OrderBy(ds => ds.Name)
            .ToListAsync();
    }

    public async Task<DataSource?> GetDataSourceWithUsageAsync(Guid dataSourceId, Guid tenantId)
    {
        return await _context.Set<DataSource>()
            .Include(ds => ds.UsageLogs.Take(10))
            .FirstOrDefaultAsync(ds => ds.Id == dataSourceId && ds.TenantId == tenantId && !ds.IsDeleted);
    }

    public async Task<bool> IsDataSourceNameExistsAsync(string name, Guid tenantId, Guid? excludeId = null)
    {
        var query = _context.Set<DataSource>()
            .Where(ds => ds.Name == name && ds.TenantId == tenantId && !ds.IsDeleted);

        if (excludeId.HasValue)
        {
            query = query.Where(ds => ds.Id != excludeId.Value);
        }

        return await query.AnyAsync();
    }

    public async Task UpdateUsageCountAsync(Guid dataSourceId)
    {
        var dataSource = await GetByIdAsync(dataSourceId);
        if (dataSource != null)
        {
            dataSource.UsageCount++;
            dataSource.LastUsed = DateTime.UtcNow;
            await UpdateAsync(dataSource);
        }
    }

    public async Task UpdateConnectionStatusAsync(Guid dataSourceId, bool isSuccess, string? errorMessage = null)
    {
        var dataSource = await GetByIdAsync(dataSourceId);
        if (dataSource != null)
        {
            dataSource.LastConnectionTest = DateTime.UtcNow;
            dataSource.LastConnectionSuccess = isSuccess;
            dataSource.LastConnectionError = errorMessage;
            await UpdateAsync(dataSource);
        }
    }

    public async Task<IEnumerable<DataSource>> GetRecentlyUsedAsync(Guid tenantId, int count = 10)
    {
        return await _context.Set<DataSource>()
            .Where(ds => ds.TenantId == tenantId && !ds.IsDeleted && ds.IsActive && ds.LastUsed.HasValue)
            .OrderByDescending(ds => ds.LastUsed)
            .Take(count)
            .ToListAsync();
    }

    public async Task<DataSource?> GetByNameAsync(string name, Guid tenantId)
    {
        return await _context.Set<DataSource>()
            .FirstOrDefaultAsync(ds => ds.Name == name && ds.TenantId == tenantId && !ds.IsDeleted);
    }
}
