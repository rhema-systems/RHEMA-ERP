using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Interfaces.Maintenance;
using Microsoft.EntityFrameworkCore;

namespace ErpSystem.Data.Repositories.Maintenance;

public class MaintenanceSettingsRepository : GenericRepository<MaintenanceSettings>, IMaintenanceSettingsRepository
{
    public MaintenanceSettingsRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<MaintenanceSettings?> GetByTenantIdAsync(Guid tenantId)
    {
        return await _dbSet.FirstOrDefaultAsync(s => s.TenantId == tenantId && !s.IsDeleted);
    }

    public async Task<MaintenanceSettings> GetOrCreateDefaultAsync(Guid tenantId, Guid userId)
    {
        var settings = await GetByTenantIdAsync(tenantId);
        if (settings != null) return settings;

        settings = new MaintenanceSettings
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            FleetComplianceDueSoonDays = 7,
            BlockFleetDispatchWhenComplianceDueSoon = true,
            DefaultFleetDefectBillingType = "Repairs",
            CreatedAt = DateTime.UtcNow,
            CreatedById = userId
        };

        await _dbSet.AddAsync(settings);
        await _context.SaveChangesAsync();

        return settings;
    }
}
