using ErpSystem.Core.DTOs.Finance;
using ErpSystem.Core.Finance.Integration;

namespace ErpSystem.Core.Interfaces.Finance;

public interface IFinancePostingEngine
{
    Task<FinancePostingResultDto> PostAsync(
        FinancePostingRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Route-aware additive posting contract.  Finance resolves the compiled route definition from
    /// the typed context and never trusts producer identity supplied in a browser/request DTO.
    /// </summary>
    Task<FinancePostingResultDto> PostAsync(
        FinancePostingRequestDto request,
        FinancePostingProducerContext producerContext,
        CancellationToken cancellationToken = default);

    Task<FinanceReversalPlanDto> GetReversalPlanAsync(
        Guid postingEventId,
        string reason,
        DateTime? reversalDate = null,
        CancellationToken cancellationToken = default);
}
