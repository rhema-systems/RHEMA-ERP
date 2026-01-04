using ErpSystem.Core.Entities.Maintenance;

namespace ErpSystem.Core.Interfaces.Maintenance;

/// <summary>
/// Repository for asset condition inspection (pre-inspection at admission / post-inspection at discharge)
/// </summary>
public interface IAssetConditionRepository
{
    // Template operations
    Task<PreInspectionChecklistTemplate?> GetTemplateByIdAsync(Guid id, Guid tenantId);
    Task<PreInspectionChecklistTemplate?> GetTemplateWithItemsAsync(Guid id, Guid tenantId);
    Task<IEnumerable<PreInspectionChecklistTemplate>> GetAllTemplatesAsync(Guid tenantId, bool includeInactive = false);
    Task<IEnumerable<PreInspectionChecklistTemplate>> GetTemplatesByAssetCategoryAsync(Guid assetCategoryId, Guid tenantId);
    Task<PreInspectionChecklistTemplate?> GetDefaultTemplateForAssetCategoryAsync(Guid assetCategoryId, Guid tenantId);
    Task<PreInspectionChecklistTemplate> AddTemplateAsync(PreInspectionChecklistTemplate template);
    Task UpdateTemplateAsync(PreInspectionChecklistTemplate template);
    Task DeleteTemplateAsync(Guid id, Guid tenantId);

    // Template Item operations
    Task<PreInspectionChecklistItem?> GetTemplateItemByIdAsync(Guid id, Guid tenantId);
    Task<IEnumerable<PreInspectionChecklistItem>> GetTemplateItemsAsync(Guid templateId, Guid tenantId);
    Task<PreInspectionChecklistItem> AddTemplateItemAsync(PreInspectionChecklistItem item);
    Task UpdateTemplateItemAsync(PreInspectionChecklistItem item);
    Task DeleteTemplateItemAsync(Guid id, Guid tenantId);
    Task DeleteTemplateItemsByTemplateIdAsync(Guid templateId, Guid tenantId);

    // Condition Record operations
    Task<AssetConditionRecord?> GetRecordByIdAsync(Guid id, Guid tenantId);
    Task<AssetConditionRecord?> GetRecordWithDetailsAsync(Guid id, Guid tenantId);
    Task<IEnumerable<AssetConditionRecord>> GetAllRecordsAsync(Guid tenantId, int page = 1, int pageSize = 20);
    Task<IEnumerable<AssetConditionRecord>> GetRecordsByAssetAsync(Guid assetId, Guid tenantId);
    Task<IEnumerable<AssetConditionRecord>> GetRecordsByAdmissionAsync(Guid admissionId, Guid tenantId);
    Task<AssetConditionRecord?> GetAdmissionRecordForAdmissionAsync(Guid admissionId, Guid tenantId);
    Task<AssetConditionRecord?> GetDischargeRecordForAdmissionAsync(Guid admissionId, Guid tenantId);
    Task<AssetConditionRecord?> GetAdmissionRecordForJobCardAsync(Guid jobCardId, Guid tenantId);
    Task<AssetConditionRecord?> GetDischargeRecordForJobCardAsync(Guid jobCardId, Guid tenantId);
    Task<int> GetRecordsCountAsync(Guid tenantId);
    Task<AssetConditionRecord> AddRecordAsync(AssetConditionRecord record);
    Task UpdateRecordAsync(AssetConditionRecord record);
    Task DeleteRecordAsync(Guid id, Guid tenantId);

    // Item Result operations
    Task<AssetConditionItemResult?> GetItemResultByIdAsync(Guid id, Guid tenantId);
    Task<IEnumerable<AssetConditionItemResult>> GetItemResultsByRecordAsync(Guid recordId, Guid tenantId);
    Task<AssetConditionItemResult> AddItemResultAsync(AssetConditionItemResult result);
    Task UpdateItemResultAsync(AssetConditionItemResult result);
    Task DeleteItemResultAsync(Guid id, Guid tenantId);
    Task<AssetConditionItemResult?> GetItemResultByRecordAndItemAsync(Guid recordId, Guid checklistItemId, Guid tenantId);
}
