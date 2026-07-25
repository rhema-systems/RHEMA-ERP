using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Procurement;

[Table("ProcurementSourcingCases")]
public sealed class ProcurementSourcingCase : TenantEntity
{
    public Guid PurchaseRequisitionId { get; set; }
    public Guid SourcingReleaseId { get; set; }
    [Range(1, int.MaxValue)] public int CaseSequence { get; set; }
    [Required, StringLength(100)] public string CaseNumber { get; set; } = string.Empty;
    public Guid SourcePlanId { get; set; }
    public Guid SourcePlanItemId { get; set; }
    public ProcurementCategoryClass Category { get; set; }
    public ProcurementMethodType RecommendedMethod { get; set; }
    public ProcurementMethodType SelectedMethod { get; set; }
    public ProcurementSourcingMethodSelectionBasis MethodSelectionBasis { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal EstimatedValue { get; set; }
    [Required, StringLength(3)] public string CurrencyCode { get; set; } = string.Empty;

    public Guid PolicySetId { get; set; }
    [Required, StringLength(50)] public string PolicyCode { get; set; } = string.Empty;
    [Range(1, int.MaxValue)] public int PolicyVersion { get; set; }
    public Guid MethodRuleId { get; set; }
    [Required, StringLength(100)] public string MethodRuleCode { get; set; } = string.Empty;
    public Guid ThresholdRuleId { get; set; }
    [Required, StringLength(100)] public string ThresholdRuleCode { get; set; } = string.Empty;
    public Guid AuthorityRouteId { get; set; }
    [Required, StringLength(100)] public string AuthorityRouteReference { get; set; } = string.Empty;

    public Guid? ApprovedExceptionRuleId { get; set; }
    public Guid? MethodOverrideWorkflowInstanceId { get; set; }
    [StringLength(1000)] public string? MethodOverrideReason { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string? MethodOverrideApprovalActorsJson { get; set; }
    public DateTime? MethodOverrideApprovedAtUtc { get; set; }
    [StringLength(200)] public string? ExceptionApprovalReference { get; set; }
    [StringLength(500)] public string? ExceptionEvidenceReference { get; set; }
    [Required, StringLength(1000)] public string Justification { get; set; } = string.Empty;
    public ProcurementSourcingCaseStatus Status { get; set; } = ProcurementSourcingCaseStatus.Ready;

    [Required, StringLength(300)] public string CreatedByName { get; set; } = string.Empty;
    public DateTime? StartedAtUtc { get; set; }
    public Guid? StartedById { get; set; }
    [StringLength(300)] public string? StartedByName { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public Guid? ClosedById { get; set; }
    [StringLength(300)] public string? ClosedByName { get; set; }
    [StringLength(500)] public string? ClosureReason { get; set; }

    [Required, StringLength(64)] public string SourceControlFingerprint { get; set; } = string.Empty;
    [Required, StringLength(64)] public string CaseFingerprint { get; set; } = string.Empty;
    [Column(TypeName = "nvarchar(max)")] public string SnapshotJson { get; set; } = string.Empty;
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public PurchaseRequisition PurchaseRequisition { get; set; } = null!;
    public ProcurementRequisitionSourcingRelease SourcingRelease { get; set; } = null!;
    public ProcurementPlan SourcePlan { get; set; } = null!;
    public ProcurementPlanItem SourcePlanItem { get; set; } = null!;
    public ProcurementPolicySet PolicySet { get; set; } = null!;
    public ProcurementPolicyMethodRule MethodRule { get; set; } = null!;
    public ProcurementPolicyThresholdRule ThresholdRule { get; set; } = null!;
    public ProcurementRequisitionAuthorityRoute AuthorityRoute { get; set; } = null!;
    public ProcurementPolicyExceptionRule? ApprovedExceptionRule { get; set; }
    public WorkflowInstance? MethodOverrideWorkflowInstance { get; set; }
    public ICollection<ProcurementSourcingCaseLot> Lots { get; set; } = new List<ProcurementSourcingCaseLot>();
    public ICollection<ProcurementSourcingCaseSourceRequest> SourceRequests { get; set; } = new List<ProcurementSourcingCaseSourceRequest>();
}

[Table("ProcurementSourcingCaseLots")]
public sealed class ProcurementSourcingCaseLot : TenantEntity
{
    public Guid SourcingCaseId { get; set; }
    [Range(1, int.MaxValue)] public int LotNumber { get; set; }
    [Required, StringLength(50)] public string LotCode { get; set; } = string.Empty;
    [Required, StringLength(200)] public string Title { get; set; } = string.Empty;
    [StringLength(2000)] public string? Description { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal EstimatedValue { get; set; }
    [Required, StringLength(3)] public string CurrencyCode { get; set; } = string.Empty;

    public ProcurementSourcingCase SourcingCase { get; set; } = null!;
    public ICollection<ProcurementSourcingCaseLotItem> Items { get; set; } = new List<ProcurementSourcingCaseLotItem>();
}

[Table("ProcurementSourcingCaseLotItems")]
public sealed class ProcurementSourcingCaseLotItem : TenantEntity
{
    public Guid SourcingCaseId { get; set; }
    public Guid LotId { get; set; }
    public Guid PurchaseRequisitionItemId { get; set; }

    public ProcurementSourcingCase SourcingCase { get; set; } = null!;
    public ProcurementSourcingCaseLot Lot { get; set; } = null!;
    public PurchaseRequisitionItem PurchaseRequisitionItem { get; set; } = null!;
}

[Table("ProcurementSourcingCaseSourceRequests")]
public sealed class ProcurementSourcingCaseSourceRequest : TenantEntity
{
    public Guid SourcingCaseId { get; set; }
    [Range(1, int.MaxValue)] public int RequestSequence { get; set; }
    [Required, StringLength(100)] public string RequestReference { get; set; } = string.Empty;
    [Required, StringLength(50)] public string SourceType { get; set; } = string.Empty;
    public ProcurementMethodType Method { get; set; }
    public ProcurementSourcingCaseSourceRequestStatus Status { get; set; } = ProcurementSourcingCaseSourceRequestStatus.Planned;
    [Column(TypeName = "nvarchar(max)")] public string LotIdsJson { get; set; } = "[]";
    public DateTime PlannedAtUtc { get; set; }
    public Guid? SourceEntityId { get; set; }
    [StringLength(100)] public string? SourceEntityReference { get; set; }
    public DateTime? RegisteredAtUtc { get; set; }
    public Guid? RegisteredById { get; set; }
    [StringLength(300)] public string? RegisteredByName { get; set; }
    [StringLength(100)] public string? CorrelationId { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ProcurementSourcingCase SourcingCase { get; set; } = null!;
}
