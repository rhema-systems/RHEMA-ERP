using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Entities.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

public interface ISubledgerSettlementReadModelService
{
    Task<SubledgerSettlementRebuildResultDto> RebuildAsync(
        SubledgerSettlementRebuildRequestDto request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SubledgerSettlementBalance>> GetBalancesAsync(
        string sourceModule,
        DateTime asOfDate,
        Guid? counterpartyId = null,
        CancellationToken cancellationToken = default);

    Task<SubledgerControlReconciliationDto> GetControlReconciliationAsync(
        string sourceModule,
        DateTime? asOfDate = null,
        CancellationToken cancellationToken = default);
}
