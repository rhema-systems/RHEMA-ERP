using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities.Finance;

namespace ErpSystem.Core.DTOs.Finance;

public sealed class JournalBatchQueryDto
{
    public JournalBatchApprovalStatus? ApprovalStatus { get; set; }
    public JournalBatchPostingStatus? PostingStatus { get; set; }
    public Guid? FiscalPeriodId { get; set; }
    public string? Search { get; set; }
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 200)] public int PageSize { get; set; } = 50;
}

public sealed class JournalBatchListResultDto
{
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public IReadOnlyList<JournalBatchListItemDto> Items { get; set; } = [];
}

public class JournalBatchListItemDto
{
    public Guid Id { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid FiscalPeriodId { get; set; }
    public string? FiscalPeriodName { get; set; }
    public string BookClassification { get; set; } = string.Empty;
    public string ControlCurrencyCode { get; set; } = string.Empty;
    public JournalBatchType BatchType { get; set; }
    public JournalBatchApprovalStatus ApprovalStatus { get; set; }
    public JournalBatchPostingStatus PostingStatus { get; set; }
    public JournalBatchReversalStatus ReversalStatus { get; set; }
    public bool IsVoided { get; set; }
    public string DisplayStatus { get; set; } = string.Empty;
    public decimal ExpectedDebitTotal { get; set; }
    public decimal ActualDebitTotal { get; set; }
    public decimal ActualCreditTotal { get; set; }
    public decimal Variance { get; set; }
    public int EntryCount { get; set; }
    public int? ExpectedJournalCount { get; set; }
    public int ApprovedEntryCount { get; set; }
    public int RejectedEntryCount { get; set; }
    public int PostedEntryCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? PostingCompletedAt { get; set; }
}

public sealed class JournalBatchDetailDto : JournalBatchListItemDto
{
    public decimal ApprovedDebitTotal { get; set; }
    public decimal RejectedDebitTotal { get; set; }
    public decimal PostedDebitTotal { get; set; }
    public decimal RemainingApprovedDebitTotal { get; set; }
    public int PendingReviewCount { get; set; }
    public int RemainingApprovedEntryCount { get; set; }
    public int LineCount { get; set; }
    public string? ContentFingerprint { get; set; }
    public string? Notes { get; set; }
    public Guid? SubmittedByUserId { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime? ReviewCompletedAt { get; set; }
    public Guid? ReversalOfJournalBatchId { get; set; }
    public Guid? ReversalBatchId { get; set; }
    public string? ReversalReason { get; set; }
    public Guid? VoidedByUserId { get; set; }
    public DateTime? VoidedAt { get; set; }
    public string? VoidReason { get; set; }
    public string RowVersion { get; set; } = string.Empty;
    public bool CanEdit { get; set; }
    public bool CanSubmit { get; set; }
    public bool CanReview { get; set; }
    public bool CanPostAny { get; set; }
    public bool CanReverseBatch { get; set; }
    public IReadOnlyList<JournalBatchItemDto> Items { get; set; } = [];
    public IReadOnlyList<JournalBatchPostingRunDto> PostingRuns { get; set; } = [];
    public IReadOnlyList<Guid> AttachmentIds { get; set; } = [];
}

public sealed class JournalBatchItemDto
{
    public Guid Id { get; set; }
    public int SequenceNumber { get; set; }
    public Guid JournalEntryId { get; set; }
    public string JournalEntryNumber { get; set; } = string.Empty;
    public DateTime EntryDate { get; set; }
    public string JournalType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public int LineCount { get; set; }
    public JournalBatchItemReviewStatus ReviewStatus { get; set; }
    public JournalBatchItemPostingStatus PostingStatus { get; set; }
    public Guid? FinalReviewedByUserId { get; set; }
    public DateTime? FinalReviewedAt { get; set; }
    public string? FinalRejectionReason { get; set; }
    public Guid? PostingClaimRunId { get; set; }
    public DateTime? PostingClaimedAt { get; set; }
    public Guid? PostedInRunId { get; set; }
    public DateTime? PostedAt { get; set; }
    public Guid? ReversalJournalBatchItemId { get; set; }
    public IReadOnlyList<JournalBatchItemReviewDto> Reviews { get; set; } = [];
}

public sealed class JournalBatchItemReviewDto
{
    public Guid Id { get; set; }
    public string WorkflowStageKey { get; set; } = string.Empty;
    public JournalBatchReviewDecision Decision { get; set; }
    public string? Comment { get; set; }
    public Guid DecidedByUserId { get; set; }
    public DateTime DecidedAt { get; set; }
}

public sealed class CreateJournalBatchDto
{
    [Required, MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public Guid FiscalPeriodId { get; set; }

    [Required, MaxLength(20)]
    public string BookClassification { get; set; } = "IFRS";

    [Required, MaxLength(3)]
    public string ControlCurrencyCode { get; set; } = "GHS";

    [Range(typeof(decimal), "0.01", "9999999999999999")]
    public decimal ExpectedDebitTotal { get; set; }

    [Range(1, 10000)]
    public int? ExpectedJournalCount { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }
}

public sealed class UpdateJournalBatchDto
{
    [Required, MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [Range(typeof(decimal), "0.01", "9999999999999999")]
    public decimal ExpectedDebitTotal { get; set; }

    [Range(1, 10000)]
    public int? ExpectedJournalCount { get; set; }

    [MaxLength(2000)]
    public string? Notes { get; set; }

    [Required]
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class AddExistingJournalToBatchDto
{
    [Required]
    public Guid JournalEntryId { get; set; }
}

public sealed class EligibleJournalBatchDraftDto
{
    public Guid Id { get; set; }
    public string JournalEntryNumber { get; set; } = string.Empty;
    public DateTime EntryDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public int LineCount { get; set; }
}

public sealed class CreateJournalBatchEntryDto
{
    [Required]
    public CreateJournalEntryDto JournalEntry { get; set; } = new();
}

public sealed class JournalBatchValidationResultDto
{
    public bool IsValid { get; set; }
    public decimal ExpectedDebitTotal { get; set; }
    public decimal ActualDebitTotal { get; set; }
    public decimal ActualCreditTotal { get; set; }
    public decimal Variance { get; set; }
    public int EntryCount { get; set; }
    public int? ExpectedJournalCount { get; set; }
    public int LineCount { get; set; }
    public IReadOnlyList<JournalBatchValidationIssueDto> Issues { get; set; } = [];
}

public sealed class JournalBatchValidationIssueDto
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Guid? JournalBatchItemId { get; set; }
    public Guid? JournalEntryId { get; set; }
    public string Severity { get; set; } = "Error";
}

public sealed class JournalBatchActionDto
{
    [MaxLength(1000)]
    public string? Comment { get; set; }
}

public sealed class JournalBatchReviewDecisionDto
{
    [Required]
    public Guid JournalBatchItemId { get; set; }

    [Required]
    public JournalBatchReviewDecision Decision { get; set; }

    [MaxLength(1000)]
    public string? Comment { get; set; }
}

public sealed class JournalBatchReviewStageDto
{
    [MinLength(1)]
    public List<JournalBatchReviewDecisionDto> Decisions { get; set; } = [];

    [MaxLength(1000)]
    public string? StageComment { get; set; }
}

public sealed class CreateJournalBatchPostingRunDto
{
    [MinLength(1)]
    public List<Guid> JournalBatchItemIds { get; set; } = [];

    [Required, MaxLength(100)]
    public string IdempotencyKey { get; set; } = string.Empty;
}

public sealed class JournalBatchPostingRunDto
{
    public Guid Id { get; set; }
    public int RunNumber { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public JournalBatchPostingRunStatus Status { get; set; }
    public Guid RequestedByUserId { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public decimal SelectedDebitTotal { get; set; }
    public int SelectedEntryCount { get; set; }
    public string? ErrorMessage { get; set; }
    public IReadOnlyList<Guid> JournalBatchItemIds { get; set; } = [];
}

public sealed class CreateJournalBatchReversalDto
{
    [Required, MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;

    [Required]
    public DateTime ReversalDate { get; set; }
}

public sealed class CopyJournalBatchDto
{
    public DateTime? EntryDate { get; set; }
    public Guid? FiscalPeriodId { get; set; }
    public bool IncludeAttachments { get; set; }
}

public sealed class JournalBatchImportIssueDto
{
    public string Sheet { get; set; } = string.Empty;
    public int Row { get; set; }
    public string? Column { get; set; }
    public string? Value { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = "Error";
}

public sealed class JournalBatchImportPreviewDto
{
    public Guid SessionId { get; set; }
    public string PreviewToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsValid { get; set; }
    public string TemplateVersion { get; set; } = "1";
    public string FileName { get; set; } = string.Empty;
    public int JournalCount { get; set; }
    public int LineCount { get; set; }
    public decimal ExpectedDebitTotal { get; set; }
    public decimal ActualDebitTotal { get; set; }
    public IReadOnlyList<JournalBatchImportIssueDto> Issues { get; set; } = [];
}

public sealed class CommitJournalBatchImportDto
{
    [Required]
    public string PreviewToken { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string IdempotencyKey { get; set; } = string.Empty;
}
