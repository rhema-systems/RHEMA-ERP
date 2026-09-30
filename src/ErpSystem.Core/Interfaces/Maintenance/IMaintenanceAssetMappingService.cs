using ErpSystem.Core.DTOs.Maintenance;
using ErpSystem.Core.Entities.Maintenance;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Interfaces.Maintenance;

/// <summary>
/// Resolves canonical Finance/Estate assets to the compatible Maintenance profile used by
/// work orders and maintenance history. The canonical registers remain the source of truth.
/// </summary>
public interface IMaintenanceAssetMappingService
{
    Task<IReadOnlyList<JobCardAssetOptionDto>> GetSelectionOptionsAsync(string? searchTerm = null);
    Task<MaintenanceAsset> ResolveOrCreateProfileAsync(
        JobCardAssetSource assetSource,
        Guid sourceAssetId,
        Guid tenantId,
        Guid? createdById = null);
}
