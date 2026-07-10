using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Workflow;

[Table("WorkflowDelegations")]
public class WorkflowDelegation : TenantEntity
{
    public Guid PrincipalUserId { get; set; }
    public Guid DelegateUserId { get; set; }
    public WorkflowDelegationKind Kind { get; set; }
    [StringLength(100)] public string? Module { get; set; }
    [StringLength(150)] public string? EntityType { get; set; }
    public Guid? WorkflowDefinitionId { get; set; }
    public Guid? WorkflowStepId { get; set; }
    public decimal? MaximumAmount { get; set; }
    [StringLength(3)] public string? CurrencyCode { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime EffectiveTo { get; set; }
    [Required, StringLength(1000)] public string Reason { get; set; } = string.Empty;
    public bool AllowRedelegation { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? RevokedAt { get; set; }
    public Guid? RevokedById { get; set; }
    [StringLength(1000)] public string? RevocationReason { get; set; }
}

[Table("WorkflowWorkingCalendars")]
public class WorkflowWorkingCalendar : TenantEntity
{
    [Required, StringLength(100)] public string Name { get; set; } = string.Empty;
    [Required, StringLength(100)] public string TimeZoneId { get; set; } = "UTC";
    public int WorkingDaysMask { get; set; } = 31;
    public TimeSpan WorkDayStart { get; set; } = new(8, 0, 0);
    public TimeSpan WorkDayEnd { get; set; } = new(17, 0, 0);
    [Column(TypeName = "nvarchar(max)")] public string HolidaysJson { get; set; } = "[]";
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
}

[Table("WorkflowCorrectionRequests")]
public class WorkflowCorrectionRequest : TenantEntity
{
    public Guid WorkflowInstanceId { get; set; }
    public Guid ApprovalId { get; set; }
    public Guid RequestedById { get; set; }
    public Guid CorrectionOwnerId { get; set; }
    public Guid? TargetStepInstanceId { get; set; }
    [Required, StringLength(2000)] public string Instructions { get; set; } = string.Empty;
    public WorkflowCorrectionStatus Status { get; set; } = WorkflowCorrectionStatus.Open;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DueAt { get; set; }
    public DateTime? ResubmittedAt { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string? ResubmissionData { get; set; }
}

[Table("WorkflowEscalationExecutions")]
public class WorkflowEscalationExecution : TenantEntity
{
    public Guid ApprovalId { get; set; }
    public int RuleIndex { get; set; }
    public WorkflowEscalationAction Action { get; set; }
    public Guid? TargetUserId { get; set; }
    [StringLength(100)] public string? TargetRole { get; set; }
    public DateTime ExecutedAt { get; set; } = DateTime.UtcNow;
    [Column(TypeName = "nvarchar(max)")] public string? Result { get; set; }
}

[Table("WorkflowEvidencePolicies")]
public class WorkflowEvidencePolicy : TenantEntity
{
    [Column(TypeName = "nvarchar(max)")] public string AllowedExtensionsJson { get; set; } = "[\".pdf\",\".doc\",\".docx\",\".xls\",\".xlsx\",\".jpg\",\".jpeg\",\".png\",\".txt\"]";
    public long MaximumFileSizeBytes { get; set; } = 25 * 1024 * 1024;
    public int RetentionDays { get; set; } = 2555;
    public bool RequireMalwareScan { get; set; } = true;
    public bool IsActive { get; set; } = true;
}

[Table("WorkflowEvidenceDocuments")]
public class WorkflowEvidenceDocument : TenantEntity
{
    public Guid StepInstanceId { get; set; }
    [Required, StringLength(64)] public string AttachmentId { get; set; } = string.Empty;
    [StringLength(200)] public string? RequirementKey { get; set; }
    [StringLength(200)] public string? ChecklistItemId { get; set; }
    [StringLength(100)] public string? DocumentType { get; set; }
    [StringLength(300)] public string? DocumentName { get; set; }
    [Required, StringLength(500)] public string FileName { get; set; } = string.Empty;
    [Required, StringLength(2000)] public string FilePath { get; set; } = string.Empty;
    [StringLength(200)] public string? ContentType { get; set; }
    public long FileSizeBytes { get; set; }
    [Required, StringLength(64)] public string Sha256 { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public Guid UploadedById { get; set; }
    public Guid DocumentOwnerId { get; set; }
    public DateTime? IssueDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public int Version { get; set; } = 1;
    public Guid? ReplacesEvidenceId { get; set; }
    public bool IsCurrent { get; set; } = true;
    public WorkflowEvidenceVerificationStatus VerificationStatus { get; set; }
    public Guid? VerifiedById { get; set; }
    public DateTime? VerifiedAt { get; set; }
    [StringLength(1000)] public string? VerificationNotes { get; set; }
    public WorkflowMalwareScanStatus MalwareScanStatus { get; set; }
    [StringLength(1000)] public string? MalwareScanResult { get; set; }
    public DateTime RetainUntil { get; set; }
    public bool IsLegalHold { get; set; }
    [StringLength(1000)] public string? LegalHoldReason { get; set; }
    public Guid? LegalHoldById { get; set; }
    public DateTime? LegalHoldAt { get; set; }
}

[Table("WorkflowSignatureEvidence")]
public class WorkflowSignatureEvidence : TenantEntity
{
    public Guid ApprovalId { get; set; }
    public Guid SignerUserId { get; set; }
    public WorkflowSignatureMethod Method { get; set; }
    [Required, StringLength(2000)] public string Attestation { get; set; } = string.Empty;
    [StringLength(200)] public string? CertificateThumbprint { get; set; }
    [StringLength(500)] public string? CertificateSubject { get; set; }
    public DateTime? CertificateNotBefore { get; set; }
    public DateTime? CertificateNotAfter { get; set; }
    public bool CertificateChainValid { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string SubmissionJson { get; set; } = "{}";
    [Required, StringLength(64)] public string SignedPayloadHash { get; set; } = string.Empty;
    public DateTime SignedAt { get; set; } = DateTime.UtcNow;
    public bool IsCommitted { get; set; }
    public DateTime? CommittedAt { get; set; }
    [StringLength(45)] public string? IpAddress { get; set; }
    [StringLength(500)] public string? UserAgent { get; set; }
}

[Table("WorkflowIntegrationExecutions")]
public class WorkflowIntegrationExecution : TenantEntity
{
    public Guid WorkflowInstanceId { get; set; }
    public Guid? StepInstanceId { get; set; }
    [Required, StringLength(200)] public string IdempotencyKey { get; set; } = string.Empty;
    [Required, StringLength(100)] public string Operation { get; set; } = string.Empty;
    [StringLength(1000)] public string? Endpoint { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string? RequestPayload { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string? ResponsePayload { get; set; }
    public WorkflowExecutionQueueStatus Status { get; set; }
    public int AttemptCount { get; set; }
    public int MaximumAttempts { get; set; } = 5;
    public DateTime? NextAttemptAt { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    [StringLength(2000)] public string? LastError { get; set; }
    public DateTime? ReconciledAt { get; set; }
    public Guid? ReconciledById { get; set; }
    [StringLength(1000)] public string? ReconciliationNotes { get; set; }
}

[Table("WorkflowOfflineActions")]
public class WorkflowOfflineAction : TenantEntity
{
    public Guid UserId { get; set; }
    [Required, StringLength(200)] public string IdempotencyKey { get; set; } = string.Empty;
    [Required, StringLength(100)] public string ActionType { get; set; } = string.Empty;
    [Column(TypeName = "nvarchar(max)")] public string Payload { get; set; } = "{}";
    public WorkflowExecutionQueueStatus Status { get; set; }
    public DateTime QueuedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    [StringLength(2000)] public string? Error { get; set; }
}
