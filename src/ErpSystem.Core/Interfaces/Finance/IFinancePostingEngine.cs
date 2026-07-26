using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

public interface IFinancePostingEngine
{
    Task<FinancePostingResultDto> PostAsync(
        FinancePostingRequestDto request,
        CancellationToken cancellationToken = default);

    Task<FinanceReversalPlanDto> GetReversalPlanAsync(
        Guid postingEventId,
        string reason,
        DateTime? reversalDate = null,
        CancellationToken cancellationToken = default);
}
