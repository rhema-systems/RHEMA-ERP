namespace ErpSystem.Core.DTOs.Finance;

/// <summary>
/// Owner-held coordinates for verifying that an already-completed producer effect is bound to the exact
/// durable Finance result. Every value is treated as an expectation; Finance reloads all authority itself.
/// </summary>
public sealed class FinanceProducerReplayVerificationRequestDto
{
    public string AccountingEventRequestFingerprint { get; set; } = string.Empty;
    public string OriginatingModuleCode { get; set; } = string.Empty;
    public string SourceDocumentType { get; set; } = string.Empty;
    public Guid SourceDocumentId { get; set; }
    public string PostingAction { get; set; } = string.Empty;
    public string ParticipantIdentity { get; set; } = string.Empty;
    public ProducerOwnerEffectReceiptDto OwnerEffectReceipt { get; set; } = new();
    public Guid FinancePostingEventId { get; set; }
    public Guid JournalEntryId { get; set; }
}

/// <summary>
/// Minimal verified replay identity. It deliberately exposes no accounting-book, line, selector or economic
/// payload; Finance has already selected and verified the compatibility representation.
/// </summary>
public sealed record FinanceProducerReplayVerificationResultDto(
    Guid AccountingEventId,
    string AccountingEventRequestFingerprint,
    string OwnerEffectFingerprint,
    string Status,
    Guid FinancePostingEventId,
    Guid JournalEntryId);
