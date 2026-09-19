namespace ErpSystem.Core.DTOs.Finance;

/// <summary>
/// Finance-selected compatibility evidence for one approved producer AccountingEvent. The result deliberately
/// contains no accounting-book identity: Finance alone selects the compatibility representation from the
/// event's immutable frozen selection.
/// </summary>
public sealed record FinanceProducerApprovedExecutionResultDto(
    Guid AccountingEventId,
    string AccountingEventRequestFingerprint,
    string Status,
    Guid FinancePostingEventId,
    Guid JournalEntryId);
