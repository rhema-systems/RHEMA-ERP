using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

/// <summary>
/// Finance-owned Core boundary for exact C7 reversal preparation. The owner supplies only deterministic
/// reversal evidence; Finance reconstructs the immutable historical economics and frozen selection.
/// </summary>
public interface IFinanceProducerReversalPreparationService
{
    Task<ProducerAccountingReversalPreparationResultDto> PrepareReversalAsync(
        PrepareProducerAccountingReversalDto request,
        CancellationToken cancellationToken = default);
}
