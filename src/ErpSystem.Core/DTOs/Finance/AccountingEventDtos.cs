namespace ErpSystem.Core.DTOs.Finance;

public sealed class CreateAccountingEventDto
{
    public Guid? AccountingEventId { get; set; }
    public string EventKind { get; set; } = "Original";
    public Guid? SupersedesAccountingEventId { get; set; }
    public Guid? CorrectsAccountingEventId { get; set; }
    public Guid? ReversesAccountingEventId { get; set; }
    public string SelectionIdempotencyKey { get; set; } = string.Empty;
    public string ExpectedCalculationInputHash { get; set; } = string.Empty;
    public string ExpectedSelectionFingerprint { get; set; } = string.Empty;
    /// <summary>Stable owner adapter identity; part of immutable C6 request evidence.</summary>
    public string ProducerParticipantIdentity { get; set; } = string.Empty;
    /// <summary>The economic command. AccountingBookCode is ignored; C5 frozen evidence is authoritative.</summary>
    public FinancePostingRequestV2Dto PostingRequest { get; set; } = new();
}

public sealed class AccountingEventDto
{
    public Guid Id { get; set; }
    public Guid RootAccountingEventId { get; set; }
    public int Version { get; set; }
    public string EventKind { get; set; } = string.Empty;
    public string OriginatingModuleCode { get; set; } = string.Empty;
    public string SourceDocumentType { get; set; } = string.Empty;
    public Guid SourceDocumentId { get; set; }
    public string PostingAction { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public Guid? SupersedesAccountingEventId { get; set; }
    public Guid? CorrectsAccountingEventId { get; set; }
    public Guid? ReversesAccountingEventId { get; set; }
    public Guid? AccountingBookSelectionEvidenceId { get; set; }
    public string SelectionFingerprint { get; set; } = string.Empty;
    public string RequestFingerprint { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public DateTime RequestedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public Guid PreparedByUserId { get; set; }
    public DateTime PreparedAtUtc { get; set; }
    public Guid? ReleasedByUserId { get; set; }
    public DateTime? ReleasedAtUtc { get; set; }
    public string? ReleaseReason { get; set; }
    public string ProducerDecisionStatus { get; set; } = string.Empty;
    public string? ProducerParticipantIdentity { get; set; }
    public Guid? ProducerDecidedByUserId { get; set; }
    public DateTime? ProducerDecidedAtUtc { get; set; }
    public string? ProducerDecisionReason { get; set; }
    public string? FailureMessage { get; set; }
    public IReadOnlyList<AccountingEventPostingDto> Postings { get; set; } = [];
    public IReadOnlyList<AccountingEventAttemptDto> Attempts { get; set; } = [];
}

public sealed class ReleaseAccountingEventDto
{
    public CreateAccountingEventDto Request { get; set; } = new();
    public string Reason { get; set; } = string.Empty;
}

public sealed class AccountingEventAttemptDto
{
    public Guid Id { get; set; }
    public int AttemptNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? FailureMessage { get; set; }
}

public sealed class AccountingEventPostingDto
{
    public int EventVersion { get; set; }
    public Guid AccountingBookId { get; set; }
    public string AccountingBookCode { get; set; } = string.Empty;
    public int SelectionOrder { get; set; }
    public string AuthorityFingerprint { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public Guid? FinancePostingEventId { get; set; }
    public Guid? JournalEntryId { get; set; }
    public DateTime? PostedAtUtc { get; set; }
    public string? FailureMessage { get; set; }
}
