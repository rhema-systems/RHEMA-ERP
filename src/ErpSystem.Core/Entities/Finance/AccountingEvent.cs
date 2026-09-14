using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// Canonical economic intent whose ordered book representations must succeed or fail together.
/// The event is Finance-owned; producer documents remain referenced by stable identity only.
/// </summary>
public sealed class AccountingEvent : TenantEntity
{
    [MaxLength(40)] public string OriginatingModuleCode { get; set; } = string.Empty;
    [MaxLength(80)] public string SourceDocumentType { get; set; } = string.Empty;
    public Guid SourceDocumentId { get; set; }
    [MaxLength(60)] public string PostingAction { get; set; } = string.Empty;
    [MaxLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [MaxLength(20)] public string EventKind { get; set; } = AccountingEventKinds.Original;
    public int Version { get; set; } = 1;
    public Guid RootAccountingEventId { get; set; }
    public Guid? SupersedesAccountingEventId { get; set; }
    public Guid? CorrectsAccountingEventId { get; set; }
    public Guid? ReversesAccountingEventId { get; set; }
    public Guid? AccountingBookSelectionEvidenceId { get; set; }
    [MaxLength(64)] public string SelectionFingerprint { get; set; } = string.Empty;
    [MaxLength(64)] public string RequestFingerprint { get; set; } = string.Empty;
    [MaxLength(20)] public string Status { get; set; } = AccountingEventStatuses.Pending;
    public DateTime EventDate { get; set; }
    public DateTime RequestedAtUtc { get; set; }
    public Guid RequestedByUserId { get; set; }
    public Guid PreparedByUserId { get; set; }
    public DateTime PreparedAtUtc { get; set; }
    public Guid? ReleasedByUserId { get; set; }
    public DateTime? ReleasedAtUtc { get; set; }
    [MaxLength(500)] public string? ReleaseReason { get; set; }
    [MaxLength(20)] public string ProducerDecisionStatus { get; set; } = ProducerIntentDecisionStatuses.NotRequired;
    [MaxLength(100)] public string? ProducerParticipantIdentity { get; set; }
    public string? ProducerIntentSnapshotJson { get; set; }
    [MaxLength(64)] public string? ProducerIntentSnapshotHash { get; set; }
    public Guid? ProducerDecidedByUserId { get; set; }
    public DateTime? ProducerDecidedAtUtc { get; set; }
    [MaxLength(500)] public string? ProducerDecisionReason { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    [MaxLength(1000)] public string? FailureMessage { get; set; }

    public AccountingEvent? RootAccountingEvent { get; set; }
    public AccountingEvent? SupersedesAccountingEvent { get; set; }
    public AccountingEvent? CorrectsAccountingEvent { get; set; }
    public AccountingEvent? ReversesAccountingEvent { get; set; }
    public AccountingBookSelectionEvidence? AccountingBookSelectionEvidence { get; set; }
    public ICollection<AccountingEventPosting> Postings { get; set; } = new List<AccountingEventPosting>();
    public ICollection<AccountingEventAttempt> Attempts { get; set; } = new List<AccountingEventAttempt>();
    public AccountingEventProducerReceipt? ProducerReceipt { get; set; }
}

/// <summary>Finance-owned immutable proof of the exact owner effect staged in the ambient transaction.</summary>
public sealed class AccountingEventProducerReceipt : TenantEntity
{
    public Guid AccountingEventId { get; set; }
    [MaxLength(100)] public string ParticipantCode { get; set; } = string.Empty;
    [MaxLength(100)] public string OwnerEntityType { get; set; } = string.Empty;
    public Guid OwnerEntityId { get; set; }
    [MaxLength(60)] public string OwnerAction { get; set; } = string.Empty;
    [MaxLength(64)] public string EffectFingerprint { get; set; } = string.Empty;
    [MaxLength(64)] public string RequestFingerprint { get; set; } = string.Empty;
    public DateTime RecordedAtUtc { get; set; }
    public Guid RecordedByUserId { get; set; }
    public AccountingEvent AccountingEvent { get; set; } = null!;
}

/// <summary>Append-only outcome of one request to release the canonical event group.</summary>
public sealed class AccountingEventAttempt : TenantEntity
{
    public Guid AccountingEventId { get; set; }
    public int AttemptNumber { get; set; }
    [MaxLength(64)] public string RequestFingerprint { get; set; } = string.Empty;
    [MaxLength(20)] public string Status { get; set; } = AccountingEventStatuses.Pending;
    public DateTime StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    [MaxLength(1000)] public string? FailureMessage { get; set; }
    public AccountingEvent AccountingEvent { get; set; } = null!;
}

/// <summary>One immutable selected-book coordinate and its resulting leaf posting evidence.</summary>
public sealed class AccountingEventPosting : TenantEntity
{
    public Guid AccountingEventId { get; set; }
    public int EventVersion { get; set; }
    public Guid AccountingBookId { get; set; }
    public int SelectionOrder { get; set; }
    [MaxLength(20)] public string AccountingBookCodeSnapshot { get; set; } = string.Empty;
    [MaxLength(64)] public string AuthorityFingerprint { get; set; } = string.Empty;
    [MaxLength(20)] public string Status { get; set; } = AccountingEventStatuses.Pending;
    public Guid? FinancePostingEventId { get; set; }
    public Guid? JournalEntryId { get; set; }
    public DateTime? PostedAtUtc { get; set; }
    [MaxLength(1000)] public string? FailureMessage { get; set; }

    public AccountingEvent AccountingEvent { get; set; } = null!;
    public AccountingBook AccountingBook { get; set; } = null!;
    public FinancePostingEvent? FinancePostingEvent { get; set; }
    public JournalEntry? JournalEntry { get; set; }
}

public static class AccountingEventKinds
{
    public const string Original = "Original";
    public const string Correction = "Correction";
    public const string Reversal = "Reversal";
}

public static class AccountingEventStatuses
{
    public const string PendingApproval = "PendingApproval";
    public const string Pending = "Pending";
    public const string Posted = "Posted";
    public const string Failed = "Failed";
}

public static class ProducerIntentDecisionStatuses
{
    public const string NotRequired = "NotRequired";
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}
