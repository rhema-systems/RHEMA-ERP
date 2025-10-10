using ErpSystem.Application.DTOs.Maintenance;

namespace ErpSystem.Application.Interfaces;

public interface IAssetTypeService
{
    Task<AssetTypeDto> CreateAssetTypeAsync(CreateAssetTypeDto dto, Guid userId, Guid tenantId);
    Task<AssetTypeDto> UpdateAssetTypeAsync(Guid id, UpdateAssetTypeDto dto, Guid userId, Guid tenantId);
    Task<bool> DeleteAssetTypeAsync(Guid id, Guid tenantId);
    Task<AssetTypeDto> GetAssetTypeByIdAsync(Guid id, Guid tenantId);
    Task<List<AssetTypeDto>> GetAssetTypesAsync(Guid tenantId, bool includeInactive = false);
    Task<List<AssetTypeDto>> GetAssetTypesByCategoryAsync(string category, Guid tenantId, bool includeInactive = false);
    Task<bool> AssetTypeExistsAsync(Guid id, Guid tenantId);
    Task<List<string>> GetAssetTypeCategoriesAsync(Guid tenantId);
}