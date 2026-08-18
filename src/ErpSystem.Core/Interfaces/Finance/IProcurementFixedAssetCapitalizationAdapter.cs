using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

/// <summary>
/// Finance-owned consumer of Procurement accepted-supply evidence.  Implementations may read the
/// producer snapshot but must never approve, reject or otherwise mutate Procurement workflow data.
/// </summary>
public interface IProcurementFixedAssetCapitalizationAdapter
{
    Task<IReadOnlyList<ProcurementFixedAssetCandidateDto>> GetCandidatesAsync(
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default);

    Task<ProcurementFixedAssetCapitalizationDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<ProcurementFixedAssetCapitalizationDto> CreateDraftAsync(
        CreateProcurementFixedAssetDraftDto dto,
        CancellationToken cancellationToken = default);

    Task<ProcurementFixedAssetCapitalizationDto> PostAsync(
        Guid id,
        PostProcurementFixedAssetCapitalizationDto dto,
        CancellationToken cancellationToken = default);
}
