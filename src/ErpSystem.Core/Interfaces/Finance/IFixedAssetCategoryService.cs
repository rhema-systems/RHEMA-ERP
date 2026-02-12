using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

public interface IFixedAssetCategoryService
{
    Task<FixedAssetCategoryDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<FixedAssetCategoryDto>> GetAllAsync();
    Task<FixedAssetCategoryDto> CreateAsync(CreateFixedAssetCategoryDto dto);
    Task<FixedAssetCategoryDto> UpdateAsync(Guid id, UpdateFixedAssetCategoryDto dto);
    Task DeleteAsync(Guid id);
}
