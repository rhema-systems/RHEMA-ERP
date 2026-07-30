using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Procurement;

[Table("ProcurementSupplierDueDiligenceReviews")]
public sealed class ProcurementSupplierDueDiligenceReview : TenantEntity
{
    public Guid BusinessPartnerId { get; set; }
    [Required, StringLength(100)] public string ReviewReference { get; set; } = string.Empty;
    public int CycleNumber { get; set; } = 1;
    public ProcurementSupplierDueDiligenceReviewType ReviewType { get; set; }
    public ProcurementSupplierDueDiligenceStatus Status { get; set; } =
        ProcurementSupplierDueDiligenceStatus.Draft;
    public ProcurementSupplierDueDiligenceOutcome Outcome { get; set; } =
        ProcurementSupplierDueDiligenceOutcome.Pending;
    public DateTime ReviewPeriodStartUtc { get; set; }
    public DateTime ReviewPeriodEndUtc { get; set; }
    public DateTime? NextReviewDueAtUtc { get; set; }

    public Guid PolicyDecisionId { get; set; }
    public Guid PolicyProfileId { get; set; }
    [Required, StringLength(50)] public string PolicyProfileCode { get; set; } = string.Empty;
    public int PolicyProfileVersion { get; set; }
    public int ReviewFrequencyMonths { get; set; }
    [Required, Column(TypeName = "nvarchar(max)")] public string PolicySnapshotJson { get; set; } = "{}";
    [Required, StringLength(64)] public string PolicyValueHash { get; set; } = string.Empty;

    public Guid WorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? SupersedesReviewId { get; set; }

    [StringLength(1000)] public string? Notes { get; set; }
    [StringLength(1000)] public string? ReviewComment { get; set; }
    public Guid? SubmittedById { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public Guid? RejectedById { get; set; }
    public DateTime? RejectedAtUtc { get; set; }
    public DateTime? ExpiredAtUtc { get; set; }
    public Guid? SupersededByReviewId { get; set; }

    [Required, StringLength(100)] public string CreationCorrelationId { get; set; } = string.Empty;
    [Required, StringLength(100)] public string LastOperationCorrelationId { get; set; } = string.Empty;
    [Required, StringLength(50)] public string LastOperation { get; set; } = "Created";
    [Required, Column(TypeName = "nvarchar(max)")] public string SnapshotJson { get; set; } = "{}";
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public BusinessPartner BusinessPartner { get; set; } = null!;
    public ProcurementConfigurationDecision PolicyDecision { get; set; } = null!;
    public ProcurementConfigurationProfile PolicyProfile { get; set; } = null!;
    public WorkflowDefinition WorkflowDefinition { get; set; } = null!;
    public WorkflowInstance? WorkflowInstance { get; set; }
    public ProcurementSupplierDueDiligenceReview? SupersedesReview { get; set; }
    public ICollection<ProcurementSupplierDueDiligenceCheck> Checks { get; set; } =
        new List<ProcurementSupplierDueDiligenceCheck>();
}

[Table("ProcurementSupplierDueDiligenceChecks")]
public sealed class ProcurementSupplierDueDiligenceCheck : TenantEntity
{
    public Guid ReviewId { get; set; }
    public ProcurementSupplierDueDiligenceCheckType CheckType { get; set; }
    public ProcurementSupplierDueDiligenceCheckStatus Status { get; set; } =
        ProcurementSupplierDueDiligenceCheckStatus.Pending;
    [Required, StringLength(200)] public string SourceName { get; set; } = string.Empty;
    [Required, StringLength(500)] public string SourceReference { get; set; } = string.Empty;
    public DateTime? CheckedAtUtc { get; set; }
    public DateTime? ValidUntilUtc { get; set; }
    public Guid? ReviewedById { get; set; }
    [StringLength(300)] public string? ReviewerName { get; set; }
    [StringLength(1000)] public string? Notes { get; set; }
    [Required, Column(TypeName = "nvarchar(max)")] public string SnapshotJson { get; set; } = "{}";
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public ProcurementSupplierDueDiligenceReview Review { get; set; } = null!;
    public ICollection<ProcurementSupplierDueDiligenceEvidenceLink> EvidenceLinks { get; set; } =
        new List<ProcurementSupplierDueDiligenceEvidenceLink>();
}

[Table("ProcurementSupplierDueDiligenceEvidenceLinks")]
public sealed class ProcurementSupplierDueDiligenceEvidenceLink : TenantEntity
{
    public Guid ReviewId { get; set; }
    public Guid CheckId { get; set; }
    public ProcurementControlEvidenceReferenceKind ReferenceKind { get; set; }
    public Guid? WorkflowEvidenceDocumentId { get; set; }
    public Guid? FileUploadRecordId { get; set; }
    [Required, StringLength(500)] public string Reference { get; set; } = string.Empty;
    [StringLength(300)] public string? Label { get; set; }
    [Required, StringLength(100)] public string RequirementKey { get; set; } = string.Empty;
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public ProcurementSupplierDueDiligenceReview Review { get; set; } = null!;
    public ProcurementSupplierDueDiligenceCheck Check { get; set; } = null!;
    public WorkflowEvidenceDocument? WorkflowEvidenceDocument { get; set; }
    public FileUploadRecord? FileUploadRecord { get; set; }
}
