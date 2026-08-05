using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities;
using ErpSystem.Core.Entities.Base;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// A durable, numbered close attempt for one fiscal period. A new cycle is created after a
/// reopen so TDC can compare the original certificate with every subsequent re-close instead
/// of overwriting the evidence attached to the first close.
/// </summary>
public class FinanceCloseCycle : TenantEntity
{
    [Required]
    public Guid FiscalPeriodId { get; set; }

    [Required]
    public int CycleNumber { get; set; }

    /// <summary>
    /// The approved template version selected when this numbered cycle began. The foreign key is
    /// retained so auditors can inspect the approved definition; copied tasks remain the runtime
    /// evidence even if the template is later superseded.
    /// </summary>
    public Guid? FinanceCloseTemplateId { get; set; }

    [Required]
    [MaxLength(40)]
    public string TemplateCode { get; set; } = "TDC-MONTH-END";

    [Required]
    [MaxLength(20)]
    public string CloseType { get; set; } = FinanceCloseTemplateTypes.MonthEnd;

    /// <summary>
    /// Version of the system task catalogue copied into this cycle. Tasks are copied rather
    /// than read live so later catalogue improvements cannot rewrite historical close evidence.
    /// </summary>
    [Required]
    public int TemplateVersion { get; set; } = 1;

    [Required]
    [MaxLength(30)]
    public string Status { get; set; } = FinanceCloseStatuses.InProgress;

    public int EvaluationCount { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public Guid? StartedByUserId { get; set; }

    [MaxLength(200)]
    public string? StartedByUserName { get; set; }

    public DateTime? LastEvaluatedAt { get; set; }
    public DateTime? PreparedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime? ReopenedAt { get; set; }
    public Guid? ReopenedByUserId { get; set; }

    [MaxLength(1000)]
    public string? ReopenReason { get; set; }

    public virtual FiscalPeriod FiscalPeriod { get; set; } = null!;
    public virtual FinanceCloseTemplate? FinanceCloseTemplate { get; set; }
    public virtual ICollection<FinanceCloseTask> Tasks { get; set; } = new List<FinanceCloseTask>();
    public virtual ICollection<FinanceCloseCheckSnapshot> CheckSnapshots { get; set; } = new List<FinanceCloseCheckSnapshot>();
    public virtual ICollection<FinanceCloseCertification> Certifications { get; set; } = new List<FinanceCloseCertification>();
    public virtual ICollection<FinanceCloseEvidenceAttachment> EvidenceAttachments { get; set; } = new List<FinanceCloseEvidenceAttachment>();
    public virtual ICollection<FinanceCloseExceptionWaiver> ExceptionWaivers { get; set; } = new List<FinanceCloseExceptionWaiver>();
    public virtual ICollection<FinanceCloseAlertDelivery> AlertDeliveries { get; set; } = new List<FinanceCloseAlertDelivery>();
    public virtual ICollection<FinancePeriodReopenRequest> ReopenRequests { get; set; } = new List<FinancePeriodReopenRequest>();
}

/// <summary>
/// The current task state inside a close cycle. Automated tasks mirror the latest check result;
/// the preparation task is completed only by a signed declaration.
/// </summary>
public class FinanceCloseTask : TenantEntity
{
    [Required]
    public Guid FinanceCloseCycleId { get; set; }

    [Required]
    [MaxLength(60)]
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

    public int Sequence { get; set; }
    public bool IsMandatory { get; set; } = true;
    public bool IsAutomated { get; set; }

    [Required]
    [MaxLength(30)]
    public string Status { get; set; } = FinanceCloseTaskStatuses.Pending;

    public Guid? AssignedToUserId { get; set; }

    [MaxLength(200)]
    public string? AssignedToUserName { get; set; }

    public DateTime? DueAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public Guid? CompletedByUserId { get; set; }

    [MaxLength(200)]
    public string? CompletedByUserName { get; set; }

    /// <summary>
    /// Human-readable evidence/comment. Automated tasks point reviewers to the immutable check
    /// snapshot; the preparation task points to its certification declaration.
    /// </summary>
    [MaxLength(2000)]
    public string? EvidenceSummary { get; set; }

    public virtual FinanceCloseCycle FinanceCloseCycle { get; set; } = null!;
    public virtual ICollection<FinanceCloseEvidenceAttachment> EvidenceAttachments { get; set; } = new List<FinanceCloseEvidenceAttachment>();
}

/// <summary>
/// Immutable result of one automated check at one evaluation number. New evaluations append
/// rows; they never edit prior rows, which is essential evidence when a blocker is later fixed.
/// </summary>
public class FinanceCloseCheckSnapshot : TenantEntity
{
    [Required]
    public Guid FinanceCloseCycleId { get; set; }

    [Required]
    public int EvaluationNumber { get; set; }

    [Required]
    [MaxLength(60)]
    public string CheckCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Category { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Severity { get; set; } = FinanceCloseCheckSeverities.Mandatory;

    [Required]
    [MaxLength(30)]
    public string Status { get; set; } = FinanceCloseCheckStatuses.Passed;

    [Required]
    [MaxLength(2000)]
    public string ResultSummary { get; set; } = string.Empty;

    public int ExceptionCount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? ExceptionAmount { get; set; }

    /// <summary>
    /// Compact JSON containing the exact validation messages and measured values used for the
    /// decision. Keeping it with the snapshot makes exported evidence independently reviewable.
    /// </summary>
    public string? EvidenceJson { get; set; }

    /// <summary>
    /// SHA-256 fingerprint of the provider result before any approved waiver is applied. A waiver
    /// can therefore be reused only while the measured exception is exactly unchanged; a changed
    /// count, amount, message or evidence payload automatically returns the control to Failed or
    /// Warning without relying on a user to remember to revoke the old approval.
    /// </summary>
    [MaxLength(64)]
    public string? EvidenceFingerprint { get; set; }

    /// <summary>
    /// Identifies the approved waiver used for this evaluation. This is intentionally a retained
    /// evidence identifier rather than a cascading relationship: historical snapshots must remain
    /// readable even if a future retention process archives waiver navigation data.
    /// </summary>
    public Guid? AppliedWaiverId { get; set; }

    public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;
    public Guid? EvaluatedByUserId { get; set; }

    [MaxLength(200)]
    public string? EvaluatedByUserName { get; set; }

    public virtual FinanceCloseCycle FinanceCloseCycle { get; set; } = null!;
}

/// <summary>
/// Links a controlled tenant upload to one close task. The uploaded object remains owned by the
/// shared file service while this row supplies Finance-specific meaning, retention and audit.
/// </summary>
public class FinanceCloseEvidenceAttachment : TenantEntity
{
    [Required]
    public Guid FinanceCloseCycleId { get; set; }

    [Required]
    public Guid FinanceCloseTaskId { get; set; }

    [Required]
    public Guid FileUploadRecordId { get; set; }

    [Required]
    [MaxLength(50)]
    public string EvidenceType { get; set; } = FinanceCloseEvidenceTypes.SupportingDocument;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public virtual FinanceCloseCycle FinanceCloseCycle { get; set; } = null!;
    public virtual FinanceCloseTask FinanceCloseTask { get; set; } = null!;
    public virtual FileUploadRecord FileUploadRecord { get; set; } = null!;
    public virtual ICollection<FinanceCloseExceptionWaiver> ExceptionWaivers { get; set; } = new List<FinanceCloseExceptionWaiver>();
}

/// <summary>
/// Controlled acceptance of one unchanged, waivable close exception. Request and review identities
/// are stored explicitly because generic audit metadata alone cannot demonstrate maker-checker.
/// </summary>
public class FinanceCloseExceptionWaiver : TenantEntity
{
    [Required]
    public Guid FinanceCloseCycleId { get; set; }

    [Required]
    public Guid FinanceCloseTaskId { get; set; }

    [Required]
    public Guid FinanceCloseCheckSnapshotId { get; set; }

    [Required]
    public Guid FinanceCloseEvidenceAttachmentId { get; set; }

    [Required]
    [MaxLength(60)]
    public string CheckCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    public string EvidenceFingerprint { get; set; } = string.Empty;

    [Required]
    [MaxLength(30)]
    public string Status { get; set; } = FinanceCloseWaiverStatuses.Requested;

    [Required]
    [MaxLength(2000)]
    public string Justification { get; set; } = string.Empty;

    public Guid RequestedByUserId { get; set; }

    [Required]
    [MaxLength(200)]
    public string RequestedByUserName { get; set; } = string.Empty;

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public Guid? ReviewedByUserId { get; set; }

    [MaxLength(200)]
    public string? ReviewedByUserName { get; set; }

    public DateTime? ReviewedAt { get; set; }

    [MaxLength(2000)]
    public string? ReviewComment { get; set; }

    public virtual FinanceCloseCycle FinanceCloseCycle { get; set; } = null!;
    public virtual FinanceCloseTask FinanceCloseTask { get; set; } = null!;
    public virtual FinanceCloseCheckSnapshot FinanceCloseCheckSnapshot { get; set; } = null!;
    public virtual FinanceCloseEvidenceAttachment FinanceCloseEvidenceAttachment { get; set; } = null!;
}

/// <summary>
/// Idempotency and audit record for one close-workspace alert sent to one user. The generic
/// Notification table remains the delivery mechanism; this Finance-owned row records why the
/// alert was due and prevents an hourly monitor or a second application node from repeatedly
/// notifying the same recipient for the same control stage.
/// </summary>
public class FinanceCloseAlertDelivery : TenantEntity
{
    [Required]
    public Guid FinanceCloseCycleId { get; set; }

    /// <summary>
    /// Present for task-aging alerts. It is nullable because waiver-review and final-close
    /// approval alerts relate to the cycle rather than to one checklist task.
    /// </summary>
    public Guid? FinanceCloseTaskId { get; set; }

    /// <summary>
    /// Present for maker-checker waiver alerts. The explicit reference lets reviewers trace an
    /// escalation to the exact immutable exception request that was awaiting a decision.
    /// </summary>
    public Guid? FinanceCloseExceptionWaiverId { get; set; }

    /// <summary>
    /// Present for period-reopen review alerts. It is separate from the cycle key because one
    /// historical closed cycle can have only one pending request but may retain several completed
    /// request decisions over its audit lifetime.
    /// </summary>
    public Guid? FinancePeriodReopenRequestId { get; set; }

    [Required]
    [MaxLength(60)]
    public string AlertType { get; set; } = string.Empty;

    /// <summary>
    /// Stable key assembled from the business event and recipient. A filtered unique index on
    /// this value is the database-level duplicate guard across retries and horizontally scaled
    /// background workers.
    /// </summary>
    [Required]
    [MaxLength(240)]
    public string DedupeKey { get; set; } = string.Empty;

    public Guid RecipientUserId { get; set; }

    [Required]
    [MaxLength(200)]
    public string RecipientUserName { get; set; } = string.Empty;

    [Required]
    [MaxLength(30)]
    public string Status { get; set; } = FinanceCloseAlertDeliveryStatuses.Pending;

    /// <summary>
    /// The accounting-control deadline that caused this alert. This is retained separately from
    /// processing timestamps so an evidence pack can show whether an alert was early, on time,
    /// or late even when the worker was temporarily unavailable.
    /// </summary>
    public DateTime DueAtUtc { get; set; }

    public DateTime? LastAttemptAtUtc { get; set; }
    public DateTime? DeliveredAtUtc { get; set; }
    public int AttemptCount { get; set; }
    public Guid? NotificationId { get; set; }

    [MaxLength(2000)]
    public string? LastError { get; set; }

    public virtual FinanceCloseCycle FinanceCloseCycle { get; set; } = null!;
    public virtual FinanceCloseTask? FinanceCloseTask { get; set; }
    public virtual FinanceCloseExceptionWaiver? FinanceCloseExceptionWaiver { get; set; }
    public virtual FinancePeriodReopenRequest? FinancePeriodReopenRequest { get; set; }
}

/// <summary>
/// Signed preparation and approval record for a cycle. The same person may not prepare and
/// approve: this enforces TDC's maker-checker default at the control boundary that closes books.
/// </summary>
public class FinanceCloseCertification : TenantEntity
{
    [Required]
    public Guid FinanceCloseCycleId { get; set; }

    public Guid? PreparedByUserId { get; set; }

    [MaxLength(200)]
    public string? PreparedByUserName { get; set; }

    public DateTime? PreparedAt { get; set; }

    [MaxLength(2000)]
    public string? PreparerDeclaration { get; set; }

    public Guid? ReviewedByUserId { get; set; }

    [MaxLength(200)]
    public string? ReviewedByUserName { get; set; }

    public DateTime? ReviewedAt { get; set; }

    [MaxLength(2000)]
    public string? ReviewerDeclaration { get; set; }

    public Guid? ApprovedByUserId { get; set; }

    [MaxLength(200)]
    public string? ApprovedByUserName { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public bool IsSuperseded { get; set; }
    public DateTime? SupersededAt { get; set; }

    [MaxLength(1000)]
    public string? SupersededReason { get; set; }

    public virtual FinanceCloseCycle FinanceCloseCycle { get; set; } = null!;
}

/// <summary>
/// Central close workflow vocabulary shared by persistence, services, tests and API consumers.
/// Strings are deliberate because they remain readable in database evidence extracts.
/// </summary>
public static class FinanceCloseStatuses
{
    public const string InProgress = "InProgress";
    public const string Prepared = "Prepared";
    public const string Closed = "Closed";
    public const string Reopened = "Reopened";
}

public static class FinanceCloseTaskStatuses
{
    public const string Pending = "Pending";
    public const string Completed = "Completed";
    public const string Blocked = "Blocked";
}

public static class FinanceCloseCheckStatuses
{
    public const string Passed = "Passed";
    public const string Failed = "Failed";
    public const string Warning = "Warning";
    public const string NotApplicable = "NotApplicable";
    public const string Waived = "Waived";
}

public static class FinanceCloseCheckSeverities
{
    public const string Mandatory = "Mandatory";
    public const string Warning = "Warning";
}

public static class FinanceCloseEvidenceTypes
{
    public const string SupportingDocument = "SupportingDocument";
    public const string Reconciliation = "Reconciliation";
    public const string ManagementApproval = "ManagementApproval";
}

public static class FinanceCloseWaiverStatuses
{
    public const string Requested = "Requested";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}

/// <summary>
/// Alert codes are persisted and included in notification metadata, so keep them stable and
/// readable. A new escalation policy stage should use a new code rather than changing the
/// meaning of an alert already retained in a historical close cycle.
/// </summary>
public static class FinanceCloseAlertTypes
{
    public const string TaskDueSoon = "TaskDueSoon";
    public const string TaskAssignmentRequired = "TaskAssignmentRequired";
    public const string TaskOverdue = "TaskOverdue";
    public const string TaskOverdueEscalation = "TaskOverdueEscalation";
    public const string WaiverReviewRequested = "WaiverReviewRequested";
    public const string WaiverReviewEscalation = "WaiverReviewEscalation";
    public const string CloseApprovalRequested = "CloseApprovalRequested";
    public const string CloseApprovalEscalation = "CloseApprovalEscalation";
    public const string PeriodReopenApprovalRequested = "PeriodReopenApprovalRequested";
    public const string PeriodReopenApprovalEscalation = "PeriodReopenApprovalEscalation";
}

public static class FinanceCloseAlertDeliveryStatuses
{
    public const string Pending = "Pending";
    public const string Delivered = "Delivered";
    public const string Failed = "Failed";
}
