using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Finance
{
    /// <summary>
    /// Request DTO for opening a period that has never been closed.
    /// Closed periods use the separate maker-checker reopen workflow because they may already
    /// support signed financial statements or other certified downstream reporting.
    /// </summary>
    public class PeriodOpenRequestDto
    {
        [Required]
        public Guid FiscalPeriodId { get; set; }

        /// <summary>
        /// Business reason retained in the Finance audit trail.
        /// </summary>
        [Required]
        [MinLength(10)]
        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Audited administrator decision controlling whether this period accepts posting dates
    /// after the current UTC business date. This does not open, close, lock, or reopen the period.
    /// </summary>
    public class PeriodPostingDatePolicyRequestDto
    {
        public bool AllowFutureDating { get; set; }

        [Required]
        [MinLength(10)]
        [MaxLength(500)]
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request DTO for closing a fiscal period
    /// </summary>
    public class PeriodCloseRequestDto
    {
        [Required]
        public Guid FiscalPeriodId { get; set; }
        
        /// <summary>
        /// Notes documenting the period close
        /// </summary>
        [MaxLength(2000)]
        public string? ClosingNotes { get; set; }

        /// <summary>
        /// The reviewer's explicit declaration. Mandatory checks cannot be bypassed; the close
        /// approver confirms that the persisted evidence was reviewed before the books close.
        /// </summary>
        [Required]
        [MinLength(20)]
        [MaxLength(2000)]
        public string ReviewerDeclaration { get; set; } = string.Empty;
    }

    /// <summary>
    /// Validation results before closing a period
    /// </summary>
    public class PeriodCloseValidationDto
    {
        public bool CanClose { get; set; }
        public List<string> ValidationErrors { get; set; } = new();
        public List<string> ValidationWarnings { get; set; } = new();
        
        // Trial balance summary
        public decimal TotalDebits { get; set; }
        public decimal TotalCredits { get; set; }
        public decimal Difference { get; set; }
        public bool IsBalanced => Math.Abs(Difference) < 0.01m;
        
        // Period info
        public string PeriodName { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalJournalEntries { get; set; }
        public int TotalTransactionLines { get; set; }
    }

    /// <summary>
    /// Result of period close operation
    /// </summary>
    public class PeriodCloseResultDto
    {
        public Guid? AccountingBookId { get; set; }
        public Guid? BookCloseCycleId { get; set; }
        public Guid? ClosingJournalEntryId { get; set; }
        public Guid? ReversalJournalEntryId { get; set; }
        public decimal? NetIncomeTransferred { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public Guid FiscalPeriodId { get; set; }
        public string PeriodName { get; set; } = string.Empty;
        public DateTime? ClosedDate { get; set; }
        public string? ClosedByUserName { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    public class PeriodClosePreparationRequestDto
    {
        [Required]
        [MinLength(20)]
        [MaxLength(2000)]
        public string Declaration { get; set; } = string.Empty;
    }

    /// <summary>
    /// Creates a new draft version. Approved versions are never edited; Finance submits a new
    /// version with the same template code when TDC's close procedure changes.
    /// </summary>
    public class SaveFinanceCloseTemplateVersionDto
    {
        [Required]
        [RegularExpression("^[A-Za-z0-9][A-Za-z0-9_-]{2,39}$")]
        public string TemplateCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(160)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string CloseType { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Description { get; set; }

        [Required]
        [MinLength(1)]
        public List<SaveFinanceCloseTemplateTaskDto> Tasks { get; set; } = new();
    }

    public class SaveFinanceCloseTemplateTaskDto
    {
        [Required]
        [RegularExpression("^[A-Za-z0-9][A-Za-z0-9_-]{2,59}$")]
        public string TaskCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Category { get; set; } = string.Empty;

        [MaxLength(60)]
        public string? DependsOnTaskCode { get; set; }

        [MaxLength(60)]
        public string? CheckCode { get; set; }

        [Range(1, 10000)]
        public int Sequence { get; set; }

        public bool IsMandatory { get; set; } = true;
        public bool IsAutomated { get; set; }

        [Range(-30, 120)]
        public int DueDaysAfterPeriodEnd { get; set; } = 5;

        public Guid? DefaultAssigneeUserId { get; set; }

        [MaxLength(2000)]
        public string? Instructions { get; set; }
    }

    public class ApproveFinanceCloseTemplateDto
    {
        [Required]
        [MinLength(20)]
        [MaxLength(2000)]
        public string Declaration { get; set; } = string.Empty;
    }

    /// <summary>
    /// Maintains a manual task inside an active cycle. Automated checks and the certification
    /// task remain owned by their dedicated service operations.
    /// </summary>
    public class UpdateFinanceCloseTaskDto
    {
        public bool AssignToCurrentUser { get; set; }
        public Guid? AssignedToUserId { get; set; }
        public DateTime? DueAt { get; set; }
        public bool MarkCompleted { get; set; }

        [MaxLength(2000)]
        public string? EvidenceSummary { get; set; }
    }

    public class LinkFinanceCloseEvidenceDto
    {
        [Required]
        public Guid FileUploadRecordId { get; set; }

        [Required]
        [MaxLength(50)]
        public string EvidenceType { get; set; } = "SupportingDocument";

        [MaxLength(1000)]
        public string? Description { get; set; }
    }

    public class RequestFinanceCloseWaiverDto
    {
        [Required]
        public Guid FinanceCloseEvidenceAttachmentId { get; set; }

        [Required]
        [MinLength(50)]
        [MaxLength(2000)]
        public string Justification { get; set; } = string.Empty;
    }

    public class ReviewFinanceCloseWaiverDto
    {
        public bool Approve { get; set; }

        [Required]
        [MinLength(20)]
        [MaxLength(2000)]
        public string Comment { get; set; } = string.Empty;
    }

    public class FinanceCloseTemplateDto
    {
        public Guid Id { get; set; }
        public string TemplateCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string CloseType { get; set; } = string.Empty;
        public int Version { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool IsSystemDefault { get; set; }
        public string? Description { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? ApprovedByUserName { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string? ApprovalDeclaration { get; set; }
        public DateTime? SupersededAt { get; set; }
        public bool CanEdit { get; set; }
        public bool CanApprove { get; set; }
        public List<FinanceCloseTemplateTaskDto> Tasks { get; set; } = new();
    }

    public class FinanceCloseTemplateTaskDto
    {
        public Guid Id { get; set; }
        public string TaskCode { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string? DependsOnTaskCode { get; set; }
        public string? CheckCode { get; set; }
        public int Sequence { get; set; }
        public bool IsMandatory { get; set; }
        public bool IsAutomated { get; set; }
        public int DueDaysAfterPeriodEnd { get; set; }
        public Guid? DefaultAssigneeUserId { get; set; }
        public string? Instructions { get; set; }
    }

    public class FinanceCloseWorkspaceDto
    {
        public Guid CycleId { get; set; }
        public Guid FiscalPeriodId { get; set; }
        public string PeriodName { get; set; } = string.Empty;
        public int CycleNumber { get; set; }
        public int TemplateVersion { get; set; }
        public string TemplateCode { get; set; } = string.Empty;
        public string CloseType { get; set; } = string.Empty;
        public string TemplateName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int EvaluationNumber { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? LastEvaluatedAt { get; set; }
        public int MandatoryBlockerCount { get; set; }
        public int WarningCount { get; set; }
        public bool CanPrepare { get; set; }
        public bool CanApproveAndClose { get; set; }
        public List<FinanceCloseTaskDto> Tasks { get; set; } = new();
        public List<FinanceCloseCheckSnapshotDto> Checks { get; set; } = new();
        public List<FinanceCloseExceptionWaiverDto> ExceptionWaivers { get; set; } = new();
        public List<FinanceCloseAlertDeliveryDto> AlertDeliveries { get; set; } = new();
        public FinanceCloseCertificationDto? Certification { get; set; }
        public List<FinanceCloseCycleHistoryDto> History { get; set; } = new();
    }

    public class FinanceCloseTaskDto
    {
        public Guid Id { get; set; }
        public string TaskCode { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string? DependsOnTaskCode { get; set; }
        public string? CheckCode { get; set; }
        public int Sequence { get; set; }
        public bool IsMandatory { get; set; }
        public bool IsAutomated { get; set; }
        public string Status { get; set; } = string.Empty;
        public Guid? AssignedToUserId { get; set; }
        public string? AssignedToUserName { get; set; }
        public DateTime? DueAt { get; set; }
        public bool IsOverdue { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? CompletedByUserName { get; set; }
        public string? EvidenceSummary { get; set; }
        public List<FinanceCloseEvidenceAttachmentDto> EvidenceAttachments { get; set; } = new();
    }

    public class FinanceCloseCheckSnapshotDto
    {
        public Guid Id { get; set; }
        public int EvaluationNumber { get; set; }
        public string CheckCode { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Severity { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string ResultSummary { get; set; } = string.Empty;
        public int ExceptionCount { get; set; }
        public decimal? ExceptionAmount { get; set; }
        public DateTime EvaluatedAt { get; set; }
        public string? EvidenceFingerprint { get; set; }
        public Guid? AppliedWaiverId { get; set; }
        public bool IsWaivable { get; set; }
    }

    public class FinanceCloseEvidenceAttachmentDto
    {
        public Guid Id { get; set; }
        public Guid FinanceCloseTaskId { get; set; }
        public Guid FileUploadRecordId { get; set; }
        public string EvidenceType { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string OriginalFileName { get; set; } = string.Empty;
        public string? ContentType { get; set; }
        public long FileSize { get; set; }
        public string FileUrl { get; set; } = string.Empty;
        public string? UploadedByUserName { get; set; }
        public DateTime UploadedAt { get; set; }
    }

    public class FinanceCloseExceptionWaiverDto
    {
        public Guid Id { get; set; }
        public Guid FinanceCloseTaskId { get; set; }
        public Guid FinanceCloseCheckSnapshotId { get; set; }
        public Guid FinanceCloseEvidenceAttachmentId { get; set; }
        public string CheckCode { get; set; } = string.Empty;
        public string EvidenceFingerprint { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Justification { get; set; } = string.Empty;
        public Guid RequestedByUserId { get; set; }
        public string RequestedByUserName { get; set; } = string.Empty;
        public DateTime RequestedAt { get; set; }
        public Guid? ReviewedByUserId { get; set; }
        public string? ReviewedByUserName { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewComment { get; set; }
        public bool MatchesLatestEvidence { get; set; }
    }

    /// <summary>
    /// Finance-specific alert audit exposed in the close workspace. The generic notification
    /// inbox remains the user's delivery surface; this projection lets close reviewers verify
    /// that overdue work and pending approvals were actually escalated.
    /// </summary>
    public class FinanceCloseAlertDeliveryDto
    {
        public Guid Id { get; set; }
        public Guid? FinanceCloseTaskId { get; set; }
        public Guid? FinanceCloseExceptionWaiverId { get; set; }
        public string AlertType { get; set; } = string.Empty;
        public Guid RecipientUserId { get; set; }
        public string RecipientUserName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime DueAtUtc { get; set; }
        public DateTime? DeliveredAtUtc { get; set; }
        public int AttemptCount { get; set; }
        public string? LastError { get; set; }
    }

    public class FinanceCloseCertificationDto
    {
        public Guid Id { get; set; }
        public string? PreparedByUserName { get; set; }
        public DateTime? PreparedAt { get; set; }
        public string? PreparerDeclaration { get; set; }
        public string? ReviewedByUserName { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewerDeclaration { get; set; }
        public string? ApprovedByUserName { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public bool IsSuperseded { get; set; }
    }

    public class FinanceCloseCycleHistoryDto
    {
        public Guid CycleId { get; set; }
        public int CycleNumber { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime StartedAt { get; set; }
        public DateTime? PreparedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public DateTime? ReopenedAt { get; set; }
        public string? ReopenReason { get; set; }
    }

    /// <summary>
    /// Request DTO for reopening a closed fiscal period
    /// </summary>
    public class PeriodReopenRequestDto
    {
        [Required]
        public Guid FiscalPeriodId { get; set; }
        
        /// <summary>
        /// Mandatory reason for reopening the period
        /// </summary>
        [Required]
        [MinLength(20)]
        [MaxLength(1000)]
        public string Reason { get; set; } = string.Empty;

        /// <summary>
        /// Required assessment of the correction and its impact on later reporting periods.
        /// This gives the independent reviewer decision-useful evidence beyond a short reason.
        /// </summary>
        [Required]
        [MinLength(20)]
        [MaxLength(2000)]
        public string AffectedPeriodAssessment { get; set; } = string.Empty;
    }

    public class PeriodReopenReviewDto
    {
        public bool Approved { get; set; }

        /// <summary>
        /// An explicit higher-tier decision declaration is required for both approval and
        /// rejection so the retained record explains the review outcome.
        /// </summary>
        [Required]
        [MinLength(20)]
        [MaxLength(2000)]
        public string ReviewComment { get; set; } = string.Empty;
    }

    public class FinancePeriodReopenRequestDto
    {
        public Guid Id { get; set; }
        public Guid FiscalPeriodId { get; set; }
        public Guid FinanceCloseCycleId { get; set; }
        public Guid? ResultingFinanceCloseCycleId { get; set; }
        public int ClosedCycleNumber { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public string AffectedPeriodAssessment { get; set; } = string.Empty;
        public string ImpactFingerprint { get; set; } = string.Empty;
        public int AffectedPeriodCount { get; set; }
        public string RequestedByUserName { get; set; } = string.Empty;
        public DateTime RequestedAt { get; set; }
        public string? ReviewedByUserName { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewComment { get; set; }
        public IReadOnlyList<FinancePeriodReopenImpactPeriodDto> AffectedPeriods { get; set; } =
            Array.Empty<FinancePeriodReopenImpactPeriodDto>();
        public IReadOnlyList<string> ValidationWarnings { get; set; } = Array.Empty<string>();
    }

    public class FinancePeriodReopenImpactPeriodDto
    {
        public Guid FiscalPeriodId { get; set; }
        public string PeriodCode { get; set; } = string.Empty;
        public string PeriodName { get; set; } = string.Empty;
        public string PeriodStatus { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request DTO for locking a fiscal period
    /// </summary>
    public class PeriodLockRequestDto
    {
        [Required]
        public Guid FiscalPeriodId { get; set; }
        
        /// <summary>
        /// Mandatory reason for locking the period
        /// </summary>
        [Required]
        [MaxLength(500)]
        public string LockReason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request DTO for unlocking a locked fiscal period.
    /// </summary>
    public class PeriodUnlockRequestDto
    {
        /// <summary>
        /// Mandatory reason for unlocking the period.
        /// </summary>
        [Required]
        [MaxLength(1000)]
        public string Reason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request DTO for year-end close
    /// </summary>
    public class YearEndCloseRequestDto
    {
        [Required]
        public Guid AccountingBookId { get; set; }

        [Required, MaxLength(100)]
        public string IdempotencyKey { get; set; } = string.Empty;

        [Required]
        public Guid FiscalYearId { get; set; }

        [Required]
        public Guid RetainedEarningsAccountId { get; set; }

        [MaxLength(2000)]
        public string? ClosingNotes { get; set; }
    }

    /// <summary>
    /// API request body for closing a fiscal year. The retained earnings account is optional
    /// here because the controller defaults it from Finance Settings when not supplied.
    /// </summary>
    public class CloseFiscalYearRequestDto
    {
        [Required]
        public Guid AccountingBookId { get; set; }

        [Required, MaxLength(100)]
        public string IdempotencyKey { get; set; } = string.Empty;

        public Guid? RetainedEarningsAccountId { get; set; }

        [MaxLength(2000)]
        public string? ClosingNotes { get; set; }
    }

    /// <summary>
    /// API request body for reopening a closed fiscal year.
    /// </summary>
    public class FiscalYearReopenRequestDto
    {
        [Required]
        public Guid AccountingBookId { get; set; }

        [Required]
        public Guid BookCloseCycleId { get; set; }

        [Required]
        [MinLength(20), MaxLength(500)]
        public string Reason { get; set; } = string.Empty;
    }

    public class YearEndBookCloseCycleDto
    {
        public Guid Id { get; set; }
        public Guid FiscalYearId { get; set; }
        public Guid AccountingBookId { get; set; }
        public string AccountingBookCode { get; set; } = string.Empty;
        public string FunctionalCurrencyCode { get; set; } = string.Empty;
        public int CycleNumber { get; set; }
        public string Status { get; set; } = string.Empty;
        public Guid? ClosingJournalEntryId { get; set; }
        public Guid? ReversalJournalEntryId { get; set; }
        public decimal NetIncomeTransferred { get; set; }
        public DateTime ClosedAtUtc { get; set; }
        public DateTime? ReopenedAtUtc { get; set; }
    }

    /// <summary>
    /// API request body for updating safe fiscal-year metadata. Dates, period structure, and
    /// close state are intentionally excluded; those change through dedicated operations.
    /// </summary>
    public class UpdateFiscalYearDto
    {
        [MaxLength(100)]
        public string? FiscalYearName { get; set; }

        [MaxLength(2000)]
        public string? Notes { get; set; }
    }
}
