using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities;

namespace ErpSystem.Core.Entities.Finance;

public enum JournalBatchApprovalStatus
{
    Draft,
    PendingApproval,
    PartiallyApproved,
    Approved,
    Rejected,
    Cancelled,
    ReadyToPost
}

public enum JournalBatchPostingStatus
{
    NotReady,
    Ready,
    Posting,
    PartiallyPosted,
    Posted
}

public enum JournalBatchReversalStatus
{
    NotReversed,
    ReversalPending,
    Reversed
}

public enum JournalBatchType
{
    Standard,
    Reversal
}

public enum JournalBatchItemReviewStatus
{
    Pending,
    Approved,
    Rejected,
    NotRequired
}

public enum JournalBatchItemPostingStatus
{
    NotEligible,
    Ready,
    Posting,
    Posted,
    Failed
}

public enum JournalBatchReviewDecision
{
    Approved,
    Rejected
}

public enum JournalBatchPostingRunStatus
{
    Pending,
    Posting,
    Posted,
    Failed
}

public enum JournalBatchImportStatus
{
    Previewed,
    Invalid,
    Committed,
    Expired,
    Failed
}

/// <summary>
/// Control header that groups independently balanced manual journals.
/// Approval, partial posting, spreadsheet provenance, and full reversal are coordinated here.
/// </summary>
public sealed class JournalBatch : TenantEntity
{
    [Required, MaxLength(50)]
    public string BatchNumber { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    public Guid FiscalPeriodId { get; set; }

    /// <summary>
    /// Stable relational identity of the governed accounting book. BookClassification is retained
    /// as the immutable code snapshot used by journal/posting audit history.
    /// </summary>
    public Guid AccountingBookId { get; set; }

    [Required, MaxLength(20)]
    public string BookClassification { get; set; } = string.Empty;

    [Required, MaxLength(3)]
    public string ControlCurrencyCode { get; set; } = "GHS";

    public JournalBatchType BatchType { get; set; } = JournalBatchType.Standard;
    public JournalBatchApprovalStatus ApprovalStatus { get; set; } = JournalBatchApprovalStatus.Draft;
    public bool ApprovalRequired { get; set; } = true;
    public JournalBatchPostingStatus PostingStatus { get; set; } = JournalBatchPostingStatus.NotReady;
    public JournalBatchReversalStatus ReversalStatus { get; set; } = JournalBatchReversalStatus.NotReversed;

    [Column(TypeName = "decimal(18,2)")]
    public decimal ExpectedDebitTotal { get; set; }

    public int? ExpectedJournalCount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? SubmittedDebitTotal { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? SubmittedCreditTotal { get; set; }

    public int? SubmittedJournalCount { get; set; }
    public int? SubmittedLineCount { get; set; }

    [MaxLength(64)]
    public string? ContentFingerprint { get; set; }

    public Guid? SubmittedByUserId { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public DateTime? ReviewCompletedAt { get; set; }
    public DateTime? PostingCompletedAt { get; set; }

    public Guid? ReversalOfJournalBatchId { get; set; }

    [MaxLength(1000)]
    public string? ReversalReason { get; set; }

    public bool IsVoided { get; set; }
    public Guid? VoidedByUserId { get; set; }
    public DateTime? VoidedAt { get; set; }

    [MaxLength(1000)]
    public string? VoidReason { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];

    [ForeignKey(nameof(FiscalPeriodId))]
    public FiscalPeriod FiscalPeriod { get; set; } = null!;

    [ForeignKey(nameof(AccountingBookId))]
    public AccountingBook AccountingBook { get; set; } = null!;

    [ForeignKey(nameof(ReversalOfJournalBatchId))]
    public JournalBatch? ReversalOfJournalBatch { get; set; }

    public ICollection<JournalBatch> ReversalAttempts { get; set; } = [];
    public ICollection<JournalBatchItem> Items { get; set; } = [];
    public ICollection<JournalBatchPostingRun> PostingRuns { get; set; } = [];
    public ICollection<JournalBatchAttachment> Attachments { get; set; } = [];
}

/// <summary>
/// Auditable membership between a journal batch and one journal entry.
/// A unique database index ensures a journal belongs to at most one active batch.
/// </summary>
public sealed class JournalBatchItem : TenantEntity
{
    public Guid JournalBatchId { get; set; }
    public Guid JournalEntryId { get; set; }
    public int SequenceNumber { get; set; }

    public JournalBatchItemReviewStatus ReviewStatus { get; set; } = JournalBatchItemReviewStatus.Pending;
    public Guid? FinalReviewedByUserId { get; set; }
    public DateTime? FinalReviewedAt { get; set; }

    [MaxLength(1000)]
    public string? FinalRejectionReason { get; set; }

    [MaxLength(64)]
    public string? SubmittedContentFingerprint { get; set; }

    public JournalBatchItemPostingStatus PostingStatus { get; set; } = JournalBatchItemPostingStatus.NotEligible;
    public Guid? PostingClaimRunId { get; set; }
    public DateTime? PostingClaimedAt { get; set; }
    public Guid? PostedInRunId { get; set; }
    public DateTime? PostedAt { get; set; }
    public Guid? ReversalJournalBatchItemId { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];

    public JournalBatch JournalBatch { get; set; } = null!;
    public JournalEntry JournalEntry { get; set; } = null!;
    public JournalBatchPostingRun? PostingClaimRun { get; set; }
    public JournalBatchPostingRun? PostedInRun { get; set; }
    public JournalBatchItem? ReversalJournalBatchItem { get; set; }
    public ICollection<JournalBatchItemReview> Reviews { get; set; } = [];
    public ICollection<JournalBatchPostingRunItem> PostingRunItems { get; set; } = [];
}

public sealed class JournalBatchItemReview : TenantEntity
{
    public Guid JournalBatchItemId { get; set; }
    public Guid WorkflowInstanceId { get; set; }
    public Guid? WorkflowStepInstanceId { get; set; }

    [Required, MaxLength(200)]
    public string WorkflowStageKey { get; set; } = string.Empty;

    public JournalBatchReviewDecision Decision { get; set; }

    [MaxLength(1000)]
    public string? Comment { get; set; }

    public Guid DecidedByUserId { get; set; }
    public DateTime DecidedAt { get; set; }

    public JournalBatchItem JournalBatchItem { get; set; } = null!;
}

public sealed class JournalBatchPostingRun : TenantEntity
{
    public Guid JournalBatchId { get; set; }
    public int RunNumber { get; set; }

    [Required, MaxLength(100)]
    public string IdempotencyKey { get; set; } = string.Empty;

    public JournalBatchPostingRunStatus Status { get; set; } = JournalBatchPostingRunStatus.Pending;
    public Guid RequestedByUserId { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal SelectedDebitTotal { get; set; }

    public int SelectedEntryCount { get; set; }

    [MaxLength(2000)]
    public string? ErrorMessage { get; set; }

    [Timestamp]
    public byte[] RowVersion { get; set; } = [];

    public JournalBatch JournalBatch { get; set; } = null!;
    public ICollection<JournalBatchPostingRunItem> Items { get; set; } = [];
}

public sealed class JournalBatchPostingRunItem : TenantEntity
{
    public Guid JournalBatchPostingRunId { get; set; }
    public Guid JournalBatchItemId { get; set; }
    public Guid? FinancePostingEventId { get; set; }

    [MaxLength(1000)]
    public string? ErrorMessage { get; set; }

    public JournalBatchPostingRun PostingRun { get; set; } = null!;
    public JournalBatchItem JournalBatchItem { get; set; } = null!;
    public FinancePostingEvent? FinancePostingEvent { get; set; }
}

[Table("JournalBatchAttachments")]
public sealed class JournalBatchAttachment : TenantEntity
{
    public Guid JournalBatchId { get; set; }
    public Guid FileUploadRecordId { get; set; }
    public JournalBatch JournalBatch { get; set; } = null!;
    public FileUploadRecord FileUploadRecord { get; set; } = null!;
}

public sealed class JournalBatchImportSession : TenantEntity
{
    [Required, MaxLength(64)]
    public string PreviewTokenHash { get; set; } = string.Empty;

    [Required, MaxLength(64)]
    public string FileHash { get; set; } = string.Empty;

    [Required, MaxLength(64)]
    public string NormalizedPayloadHash { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string TemplateVersion { get; set; } = "2";

    [Required, MaxLength(260)]
    public string OriginalFileName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? IdempotencyKey { get; set; }

    public JournalBatchImportStatus Status { get; set; }
    public Guid UploadedByUserId { get; set; }
    public DateTime ExpiresAt { get; set; }
    public int JournalCount { get; set; }
    public int LineCount { get; set; }
    public int ErrorCount { get; set; }
    public Guid? CommittedJournalBatchId { get; set; }

    [Required, Column(TypeName = "nvarchar(max)")]
    public string NormalizedPayloadJson { get; set; } = "{}";

    [Required, Column(TypeName = "nvarchar(max)")]
    public string IssuesJson { get; set; } = "[]";

    [MaxLength(2000)]
    public string? ErrorMessage { get; set; }

    public JournalBatch? CommittedJournalBatch { get; set; }
}
