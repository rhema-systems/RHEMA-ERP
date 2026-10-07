using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

public interface IFinanceExchangeRateOverrideService
{
    Task<IReadOnlyList<FinanceExchangeRateOverrideRequestDto>> GetForSourceAsync(
        string sourceDocumentType, Guid sourceDocumentId, CancellationToken cancellationToken = default);
    Task<FinanceExchangeRateOverrideRequestDto> RequestAsync(
        FinanceExchangeRateOverrideCommandDto command, CancellationToken cancellationToken = default);
    Task ApplyWorkflowOutcomeAsync(
        Guid requestId, bool approved, Guid actorUserId, string? reason, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> ApplyApprovedOverridesForPostingAsync(
        Guid tenantId, FinancePostingCommandDto posting, CancellationToken cancellationToken = default);
    Task ConsumeAsync(
        Guid tenantId, IReadOnlyList<Guid> requestIds, Guid postingEventId, CancellationToken cancellationToken = default);
}
