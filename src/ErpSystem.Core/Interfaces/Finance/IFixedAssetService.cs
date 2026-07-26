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
    Task<BulkImportResultDto> ImportAssetsFromExcelAsync(Stream fileStream, string fileName, bool dryRun = false);
    Task<byte[]> GenerateImportTemplateAsync();

    // Lifecycle Management
    Task<FixedAssetDto> SubmitCapitalizationForApprovalAsync(Guid id, string? comments = null, CancellationToken cancellationToken = default);
    Task<FixedAssetDto> CapitalizeAsync(Guid id, CapitalizeFixedAssetDto dto);
    Task<FixedAssetDto> ActivateAsync(Guid id, DateTime? placedInServiceDate);
    Task<FixedAssetDto> PutOnHoldAsync(Guid id, string reason);
    Task<FixedAssetDto> ResumeAsync(Guid id);

    // Source-document capitalization back-reference maintenance
    Task RecordApInvoiceCapitalizationAsync(
        Guid vendorInvoiceId,
        Guid journalEntryId,
        Guid postingEventId,
        CancellationToken cancellationToken = default);

    // Dashboard
    Task<FixedAssetDashboardDto> GetDashboardAsync();

    // Asset Code Generation
    Task<string> GenerateAssetCodeAsync(Guid categoryId);
}
