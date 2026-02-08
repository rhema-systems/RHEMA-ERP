using ErpSystem.Core.DTOs.Procurement;

namespace ErpSystem.Core.Interfaces.Procurement;

/// <summary>
/// Service interface for procurement settings management
/// </summary>
public interface IProcurementSettingsService
{
    /// <summary>
    /// Get procurement settings for current tenant
    /// </summary>
    Task<ProcurementSettingsDto> GetSettingsAsync();

    /// <summary>
    /// Update procurement settings
    /// </summary>
    Task<ProcurementSettingsDto> UpdateSettingsAsync(UpdateProcurementSettingsDto dto);

    /// <summary>
    /// Check if auto-create inventory items is enabled
    /// </summary>
    Task<bool> ShouldAutoCreateInventoryItemsAsync();

    /// <summary>
    /// Check if auto-create supplier items is enabled
    /// </summary>
    Task<bool> ShouldAutoCreateSupplierItemsAsync();

    /// <summary>
    /// Check if non-inventory items are allowed
    /// </summary>
    Task<bool> AllowNonInventoryItemsAsync();
}
