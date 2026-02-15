using ErpSystem.Core.Entities.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

/// <summary>
/// Repository interface for procurement settings
/// </summary>
public interface IProcurementSettingsRepository : IGenericRepository<ProcurementSettings>
{
    /// <summary>
    /// Get settings by tenant ID
    /// </summary>
    Task<ProcurementSettings?> GetByTenantIdAsync(Guid tenantId);

    /// <summary>
    /// Get settings for tenant, or create default if not exists
    /// </summary>
    Task<ProcurementSettings> GetOrCreateDefaultAsync(Guid tenantId, Guid userId);
}
