using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Procurement;

[Table("ProcurementMasterDataControlPolicies")]
public sealed class ProcurementMasterDataControlPolicy : TenantEntity
{
    public Guid PolicyKey { get; set; } = Guid.NewGuid();
    public ProcurementMasterDataResourceType ResourceType { get; set; }
    public int Version { get; set; } = 1;
    public ProcurementMasterDataPolicyStatus Status { get; set; } = ProcurementMasterDataPolicyStatus.Draft;
    [Required, StringLength(200)] public string Name { get; set; } = string.Empty;
    [StringLength(1000)] public string? Description { get; set; }
    [Required, Column(TypeName = "nvarchar(max)")] public string MakerRolesJson { get; set; } = "[]";
    [Required, Column(TypeName = "nvarchar(max)")] public string CheckerRolesJson { get; set; } = "[]";
    public bool RequireIndependentApproval { get; set; } = true;
    public bool RequireRevalidation { get; set; } = true;
    public bool RequireEvidence { get; set; }
    public Guid? WorkflowDefinitionId { get; set; }
    public DateTime EffectiveFromUtc { get; set; }
    public DateTime? EffectiveToUtc { get; set; }
    public Guid? SupersedesPolicyId { get; set; }
    public Guid? ActivatedById { get; set; }
    public DateTime? ActivatedAtUtc { get; set; }
    public Guid? RetiredById { get; set; }
    public DateTime? RetiredAtUtc { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public WorkflowDefinition? WorkflowDefinition { get; set; }
}

[Table("ProcurementMasterDataChangeRequests")]
public sealed class ProcurementMasterDataChangeRequest : TenantEntity
{
    [Required, StringLength(50)] public string RequestNumber { get; set; } = string.Empty;
    public Guid PolicyId { get; set; }
    public int PolicyVersion { get; set; }
    public ProcurementMasterDataResourceType ResourceType { get; set; }
    public ProcurementMasterDataTargetKind TargetKind { get; set; }
    public Guid TargetId { get; set; }
    [Required, StringLength(300)] public string TargetReference { get; set; } = string.Empty;
    public ProcurementMasterDataChangeStatus Status { get; set; } = ProcurementMasterDataChangeStatus.Draft;
    [Required, Column(TypeName = "nvarchar(max)")] public string BeforeJson { get; set; } = "{}";
    [Required, StringLength(64)] public string BeforeHash { get; set; } = string.Empty;
    [Required, Column(TypeName = "nvarchar(max)")] public string ProposedChangesJson { get; set; } = "{}";
    [Required, StringLength(64)] public string ProposedChangesHash { get; set; } = string.Empty;
    [Column(TypeName = "nvarchar(max)")] public string? AppliedAfterJson { get; set; }
    [StringLength(64)] public string? AppliedAfterHash { get; set; }
    [Required, StringLength(2000)] public string Reason { get; set; } = string.Empty;
    public DateTime EffectiveAtUtc { get; set; }
    public Guid MakerUserId { get; set; }
    public Guid? SubmittedById { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public Guid? CheckerUserId { get; set; }
    public DateTime? CheckedAtUtc { get; set; }
    [StringLength(2000)] public string? CheckerComment { get; set; }
    public Guid? RevalidatedById { get; set; }
    public DateTime? RevalidatedAtUtc { get; set; }
    public bool? RevalidationPassed { get; set; }
    [StringLength(2000)] public string? RevalidationMessage { get; set; }
    [StringLength(64)] public string? RevalidatedSnapshotHash { get; set; }
    public Guid? AppliedById { get; set; }
    public DateTime? AppliedAtUtc { get; set; }
    public Guid? WorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ProcurementMasterDataControlPolicy Policy { get; set; } = null!;
    public WorkflowDefinition? WorkflowDefinition { get; set; }
    public WorkflowInstance? WorkflowInstance { get; set; }
    public ICollection<ProcurementMasterDataChangeEvidenceLink> EvidenceLinks { get; set; } = new List<ProcurementMasterDataChangeEvidenceLink>();
}

[Table("ProcurementMasterDataChangeEvidenceLinks")]
public sealed class ProcurementMasterDataChangeEvidenceLink : TenantEntity
{
    public Guid ChangeRequestId { get; set; }
    public ProcurementControlEvidenceReferenceKind ReferenceKind { get; set; }
    public Guid? WorkflowEvidenceDocumentId { get; set; }
    public Guid? FileUploadRecordId { get; set; }
    [Required, StringLength(200)] public string Reference { get; set; } = string.Empty;
    [StringLength(300)] public string? Label { get; set; }
    [StringLength(200)] public string? RequirementKey { get; set; }

    public ProcurementMasterDataChangeRequest ChangeRequest { get; set; } = null!;
    public WorkflowEvidenceDocument? WorkflowEvidenceDocument { get; set; }
    public FileUploadRecord? FileUploadRecord { get; set; }
}
