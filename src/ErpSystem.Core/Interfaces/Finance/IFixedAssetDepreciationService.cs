using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

public interface IFixedAssetDepreciationService
{
    Task<IReadOnlyList<AssetDepreciationScheduleDto>> RunDepreciationAsync(RunDepreciationDto dto, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AssetDepreciationScheduleDto>> GetSchedulesForAssetAsync(Guid fixedAssetId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AssetDepreciationScheduleDto>> GetSchedulesForPeriodAsync(Guid fiscalPeriodId, CancellationToken cancellationToken = default);
}
