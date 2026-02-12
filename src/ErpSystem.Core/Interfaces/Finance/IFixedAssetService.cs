using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

public interface IFixedAssetService
{
    Task<FixedAssetDto?> GetByIdAsync(Guid id);
    Task<IEnumerable<FixedAssetDto>> GetAllAsync();
    Task<FixedAssetDto> CreateAsync(CreateFixedAssetDto dto);
    Task<FixedAssetDto> UpdateAsync(Guid id, UpdateFixedAssetDto dto);
    Task DeleteAsync(Guid id);
    
    // Bulk Import
    Task<BulkImportResultDto> ImportAssetsFromExcelAsync(Stream fileStream, string fileName);
    Task<byte[]> GenerateImportTemplateAsync();
}
