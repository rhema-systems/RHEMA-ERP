using ErpSystem.Core.DTOs.Finance;

namespace ErpSystem.Core.Interfaces.Finance;

public interface IFinanceBudgetControlService
{
    Task<FinanceBudgetControlEvaluationDto> EvaluateManualJournalAsync(Guid journalEntryId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> ReserveManualJournalAsync(Guid journalEntryId, CancellationToken cancellationToken = default);
    Task ReleaseManualJournalAsync(Guid journalEntryId, string reason, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> ValidateManualJournalForPostingAsync(Guid journalEntryId, CancellationToken cancellationToken = default);
    Task ConsumeReservationsAsync(Guid tenantId, Guid sourceDocumentId, IReadOnlyList<Guid> reservationIds, Guid journalEntryId, Guid postingEventId, CancellationToken cancellationToken = default);
    Task<FinanceBudgetOverrideRequestDto> RequestManualJournalOverrideAsync(Guid journalEntryId, string reason, CancellationToken cancellationToken = default);
    Task InvalidateManualJournalOverridesAsync(Guid journalEntryId, string reason, CancellationToken cancellationToken = default);
    Task ApplyOverrideOutcomeAsync(Guid overrideRequestId, bool approved, Guid actorUserId, string? reason, CancellationToken cancellationToken = default);
}
