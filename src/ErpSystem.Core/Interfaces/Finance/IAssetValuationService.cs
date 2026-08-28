using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

public interface IAssetValuationService
{
    /// <summary>
    /// Create a single asset revaluation or impairment
    /// </summary>
    Task<AssetValuationDto> CreateValuationAsync(CreateAssetValuationDto dto, Guid performedByUserId);

    /// <summary>
    /// Create bulk revaluations using an index percentage
    /// </summary>
    Task<BulkOperationResultDto<AssetValuationDto>> CreateBulkValuationAsync(CreateBulkAssetValuationDto dto, Guid performedByUserId);

    /// <summary>
    /// Get valuation history for an asset
    /// </summary>
    Task<IEnumerable<AssetValuationDto>> GetValuationsByAssetAsync(Guid assetId);

    /// <summary>
    /// Post a valuation to the General Ledger (creates journal entry)
    /// </summary>
    Task<AssetValuationDto> PostValuationToGLAsync(Guid valuationId);

    Task<IReadOnlyList<AssetValuationCorrectionDto>> GetCorrectionsAsync(
        Guid valuationId, CancellationToken cancellationToken = default);
    Task<AssetValuationCorrectionDto> RequestCorrectionAsync(
        Guid valuationId, RequestAssetValuationCorrectionDto dto, CancellationToken cancellationToken = default);
    Task<AssetValuationCorrectionDto> ReviewCorrectionAsync(
        Guid valuationId, Guid correctionId, ReviewAssetValuationCorrectionDto dto, CancellationToken cancellationToken = default);
    Task<AssetValuationCorrectionDto> PostCorrectionAsync(
        Guid valuationId, Guid correctionId, CancellationToken cancellationToken = default);
}
