using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Workflow;

namespace ErpSystem.Core.Entities.Procurement;

/// <summary>
/// Immutable evidence that a purchase requisition passed every control required before sourcing.
/// A new row is appended when the underlying control fingerprint changes; released rows are never edited.
/// </summary>
[Table("ProcurementRequisitionSourcingReleases")]
public sealed class ProcurementRequisitionSourcingRelease : TenantEntity
{
    public Guid PurchaseRequisitionId { get; set; }
    [Range(1, int.MaxValue)] public int AttemptNumber { get; set; }
    [Required, StringLength(100)] public string ReleaseReference { get; set; } = string.Empty;

    public Guid SourcePlanId { get; set; }
    public Guid SourcePlanItemId { get; set; }
    public Guid? AppSubmissionId { get; set; }
    public int? AppSubmissionAttemptNumber { get; set; }
    [StringLength(100)] public string? AppAcknowledgementReference { get; set; }
    public Guid? ApprovedExceptionRuleId { get; set; }
    public Guid? ExceptionWorkflowInstanceId { get; set; }
    [StringLength(200)] public string? ExceptionApprovalReference { get; set; }

    public Guid SpecificationTemplateId { get; set; }
    [Required, StringLength(50)] public string SpecificationTemplateCode { get; set; } = string.Empty;
    [Range(1, int.MaxValue)] public int SpecificationTemplateVersion { get; set; }

    public Guid BudgetCommitmentId { get; set; }
    [Required, StringLength(100)] public string BudgetCommitmentReference { get; set; } = string.Empty;
    public Guid AuthorityRouteId { get; set; }
    [Required, StringLength(100)] public string AuthorityRouteReference { get; set; } = string.Empty;
    public Guid WorkflowInstanceId { get; set; }

    public DateTime ReleasedAtUtc { get; set; }
    public Guid ReleasedById { get; set; }
    [Required, StringLength(300)] public string ReleasedByName { get; set; } = string.Empty;
    [Required, StringLength(500)] public string ReleaseReason { get; set; } = string.Empty;
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Required, StringLength(64)] public string ControlFingerprint { get; set; } = string.Empty;
    [Column(TypeName = "nvarchar(max)")] public string SnapshotJson { get; set; } = string.Empty;
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public PurchaseRequisition PurchaseRequisition { get; set; } = null!;
    public ProcurementAppSubmission? AppSubmission { get; set; }
    public ProcurementPolicyExceptionRule? ApprovedExceptionRule { get; set; }
    public WorkflowInstance? ExceptionWorkflowInstance { get; set; }
    public ProcurementSpecificationTemplate SpecificationTemplate { get; set; } = null!;
    public ProcurementBudgetCommitment BudgetCommitment { get; set; } = null!;
    public ProcurementRequisitionAuthorityRoute AuthorityRoute { get; set; } = null!;
    public WorkflowInstance WorkflowInstance { get; set; } = null!;
    public ICollection<ProcurementSourcingCase> SourcingCases { get; set; } = new List<ProcurementSourcingCase>();
}
