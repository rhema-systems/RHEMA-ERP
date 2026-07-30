using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Procurement;

[Table("ProcurementContractActivations")]
public sealed class ProcurementContractActivation : TenantEntity
{
    public Guid ContractId { get; set; }
    [Range(1, int.MaxValue)] public int Sequence { get; set; }
    public ProcurementContractActivationStatus Status { get; set; }
    public Guid ConfigurationProfileId { get; set; }
    public int ConfigurationProfileVersion { get; set; }
    public Guid PolicySetId { get; set; }
    public int PolicyVersion { get; set; }
    public Guid AuthorityRuleId { get; set; }
    [Required, StringLength(200)] public string AuthorityName { get; set; } = string.Empty;
    public Guid WorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public Guid AwardReadinessDecisionId { get; set; }
    public int AwardReadinessSequence { get; set; }
    [Required, StringLength(64)] public string AwardReadinessIntegrityHash { get; set; } = string.Empty;
    public Guid GhanepsConfigurationDecisionId { get; set; }
    [Required, StringLength(64)] public string GhanepsConfigurationValueHash { get; set; } = string.Empty;
    public bool GhanepsRequired { get; set; }
    public bool GhanepsCompliant { get; set; }
    public bool PerformanceSecurityRequired { get; set; }
    public Guid? PerformanceBondRequestId { get; set; }
    public Guid SubmittedById { get; set; }
    [Required, StringLength(300)] public string SubmittedByName { get; set; } = string.Empty;
    public DateTime SubmittedAtUtc { get; set; }
    public Guid? DecidedById { get; set; }
    [StringLength(300)] public string? DecidedByName { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public Guid? ActivatedById { get; set; }
    [StringLength(300)] public string? ActivatedByName { get; set; }
    public DateTime? ActivatedAtUtc { get; set; }
    [Required, StringLength(1000)] public string Reason { get; set; } = string.Empty;
    [StringLength(1000)] public string? DecisionComment { get; set; }
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Required, Column(TypeName = "nvarchar(max)")] public string ContractSnapshotJson { get; set; } = "{}";
    [Required, StringLength(64)] public string ContractSnapshotHash { get; set; } = string.Empty;
    [Required, Column(TypeName = "nvarchar(max)")] public string ReadinessSnapshotJson { get; set; } = "{}";
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public Contract Contract { get; set; } = null!;
    public WorkflowDefinition WorkflowDefinition { get; set; } = null!;
    public WorkflowInstance? WorkflowInstance { get; set; }
    public ProcurementAwardReadinessDecision AwardReadinessDecision { get; set; } = null!;
    public PerformanceBondRequest? PerformanceBondRequest { get; set; }
    public ICollection<ProcurementContractActivationEvidence> Evidence { get; set; } =
        new List<ProcurementContractActivationEvidence>();
}

[Table("ProcurementContractActivationEvidence")]
public sealed class ProcurementContractActivationEvidence : TenantEntity
{
    public Guid ActivationId { get; set; }
    [Required, StringLength(200)] public string RequirementKey { get; set; } = string.Empty;
    [Required, StringLength(300)] public string RequirementLabel { get; set; } = string.Empty;
    public ProcurementContractActivationEvidenceKind ReferenceKind { get; set; }
    public Guid? WorkflowEvidenceDocumentId { get; set; }
    public Guid? FileUploadRecordId { get; set; }
    [Required, StringLength(1000)] public string EvidenceReference { get; set; } = string.Empty;
    [Required, StringLength(64)] public string EvidenceHash { get; set; } = string.Empty;

    public ProcurementContractActivation Activation { get; set; } = null!;
    public WorkflowEvidenceDocument? WorkflowEvidenceDocument { get; set; }
    public FileUploadRecord? FileUploadRecord { get; set; }
}
