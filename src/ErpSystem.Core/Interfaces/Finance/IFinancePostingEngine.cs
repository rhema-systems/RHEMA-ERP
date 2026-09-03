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

    Task<FinancePostingResultDto> PostAsync(
        FinancePostingRequestV2Dto request,
        CancellationToken cancellationToken = default);

    Task<FinancePostingResultDto> PostAsync(
        FinancePostingRequestV2Dto request,
        FinancePostingProducerContext producerContext,
        CancellationToken cancellationToken = default);

    Task<FinanceReversalPlanDto> GetReversalPlanAsync(
        Guid postingEventId,
        string reason,
        DateTime? reversalDate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Posts an exact reversal derived exclusively from immutable Finance posting evidence.
    /// Caller-supplied lines and historical-mapping bypass flags are intentionally absent.
    /// </summary>
    Task<FinancePostingResultDto> ReverseAsync(
        Guid postingEventId,
        string reason,
        DateTime? reversalDate = null,
        CancellationToken cancellationToken = default);
}
