using ErpSystem.Core.DTOs.Maintenance;

namespace ErpSystem.Core.Interfaces.Maintenance;

/// <summary>
/// Service for managing asset condition inspections (pre-inspection at admission / post-inspection at discharge)
/// </summary>
public interface IAssetConditionService
{
    // Template operations
    Task<AssetConditionChecklistTemplateDto> GetTemplateByIdAsync(Guid id);
    Task<AssetConditionChecklistTemplateDto> GetTemplateWithItemsAsync(Guid id);
    Task<IEnumerable<AssetConditionChecklistTemplateDto>> GetAllTemplatesAsync(bool includeInactive = false);
    Task<IEnumerable<AssetConditionChecklistTemplateDto>> GetTemplatesByAssetCategoryAsync(Guid assetCategoryId);
    Task<AssetConditionChecklistTemplateDto?> GetDefaultTemplateForAssetCategoryAsync(Guid assetCategoryId);
    Task<AssetConditionChecklistTemplateDto> CreateTemplateAsync(CreateAssetConditionTemplateDto dto);
    Task<AssetConditionChecklistTemplateDto> UpdateTemplateAsync(Guid id, UpdateAssetConditionTemplateDto dto);
    Task DeleteTemplateAsync(Guid id);

    // Condition Record operations
    Task<AssetConditionRecordDto> GetRecordByIdAsync(Guid id);
    Task<AssetConditionRecordDto> GetRecordWithDetailsAsync(Guid id);
    Task<IEnumerable<AssetConditionRecordSummaryDto>> GetAllRecordsAsync(int page = 1, int pageSize = 20);
    Task<IEnumerable<AssetConditionRecordSummaryDto>> GetRecordsByAssetAsync(Guid assetId);
    Task<AssetConditionRecordDto?> GetAdmissionRecordForAdmissionAsync(Guid admissionId);
    Task<AssetConditionRecordDto?> GetDischargeRecordForAdmissionAsync(Guid admissionId);
    Task<AssetConditionRecordDto?> GetAdmissionRecordForJobCardAsync(Guid jobCardId);
    Task<AssetConditionRecordDto?> GetDischargeRecordForJobCardAsync(Guid jobCardId);
    Task<AssetConditionRecordDto> StartConditionInspectionAsync(CreateAssetConditionRecordDto dto);
    Task<AssetConditionItemResultDto> SubmitItemResultAsync(Guid recordId, SubmitAssetConditionItemDto dto);
    Task<AssetConditionRecordDto> CompleteConditionInspectionAsync(Guid recordId, CompleteAssetConditionRecordDto dto);
    Task CancelConditionInspectionAsync(Guid recordId);
    Task LinkToAdmissionAsync(Guid recordId, Guid admissionId);
    Task LinkToDischargeAsync(Guid recordId, Guid dischargeId);
    Task<AssetConditionRecordDto> UploadItemPhotoAsync(Guid recordId, Guid itemResultId, string photoPath);
}
