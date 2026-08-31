using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Finance.Integration;

namespace ErpSystem.Core.Interfaces.Finance;

/// <summary>
/// Finance-owned adapter boundary for exact settlement allocation evidence. Route services load
/// their own authoritative entities, then supply trusted stable ids and frozen source evidence.
/// </summary>
public interface IFinanceSettlementDimensionService
{
    Task<IReadOnlyList<FinanceSettlementDimensionComponentDto>> SynchronizeDraftAsync(
        FinancePostingProducerContext producer,
        Guid sourceDocumentId,
        IReadOnlyList<FinanceSettlementAllocationInput> allocations,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FinanceSettlementDimensionComponentDto>> GetAsync(
        FinancePostingProducerContext producer,
        Guid sourceDocumentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FinanceSettlementDimensionComponentDto>> ValidateAndFreezeAsync(
        FinancePostingProducerContext producer,
        Guid sourceDocumentId,
        IReadOnlyCollection<Guid> authoritativeSettlementSourceLineIds,
        CancellationToken cancellationToken = default);
}
