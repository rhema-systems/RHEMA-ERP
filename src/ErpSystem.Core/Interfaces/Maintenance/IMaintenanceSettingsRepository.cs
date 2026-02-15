using ErpSystem.Core.Entities.Maintenance;

namespace ErpSystem.Core.Interfaces.Maintenance;

public interface IMaintenanceSettingsRepository : IGenericRepository<MaintenanceSettings>
{
    Task<MaintenanceSettings?> GetByTenantIdAsync(Guid tenantId);
    Task<MaintenanceSettings> GetOrCreateDefaultAsync(Guid tenantId, Guid userId);
}

