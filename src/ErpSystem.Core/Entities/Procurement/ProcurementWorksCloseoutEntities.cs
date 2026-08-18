using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Procurement;

[Table("ProcurementWorksCloseoutActions")]
public sealed class ProcurementWorksCloseoutAction : TenantEntity
{
    public Guid ContractId { get; set; }
    public Guid ProjectId { get; set; }
    [Range(1, int.MaxValue)] public int Sequence { get; set; }
    public ProcurementWorksCloseoutActionType ActionType { get; set; }
    public ProcurementWorksCloseoutActionStatus Status { get; set; }
    public Guid ConfigurationProfileId { get; set; }
    public int ConfigurationProfileVersion { get; set; }
    public Guid PolicySetId { get; set; }
    public int PolicyVersion { get; set; }
    public Guid AuthorityRuleId { get; set; }
    [Required, StringLength(200)] public string AuthorityName { get; set; } = string.Empty;
    public Guid WorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? ProjectHandoverItemId { get; set; }
    public Guid? ProjectDefectLiabilityCaseId { get; set; }
    public Guid? ProjectFinalAccountId { get; set; }
    public Guid? ProjectPaymentCertificateId { get; set; }
    public Guid? PerformanceBondRequestId { get; set; }
    public ProcurementRetentionReleaseStage? RetentionReleaseStage { get; set; }
    public Guid? QuantitySurveyConfigurationProfileId { get; set; }
    public int? QuantitySurveyConfigurationProfileVersion { get; set; }
    public Guid? QuantitySurveyRetentionDecisionId { get; set; }
    [StringLength(64)] public string? QuantitySurveyRetentionPolicyHash { get; set; }
    [StringLength(64)] public string? RequestHash { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal? RetentionHeldSnapshot { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal? RetentionReleasedBefore { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal? RetentionStageLimitAmount { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal? RetentionReleasedAfter { get; set; }
    public bool UsesRetentionBond { get; set; }
    public DateTime? EffectiveAtUtc { get; set; }
    public DateTime? DefectsLiabilityEndsAtUtc { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal? Amount { get; set; }
    [StringLength(3)] public string? Currency { get; set; }
    public bool RequiresIndependentFinanceApproval { get; set; }
    public bool AmountAutoPosted { get; set; }
    public Guid SubmittedById { get; set; }
    [Required, StringLength(300)] public string SubmittedByName { get; set; } = string.Empty;
    public DateTime SubmittedAtUtc { get; set; }
    public Guid? DecidedById { get; set; }
    [StringLength(300)] public string? DecidedByName { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    [Required, StringLength(1000)] public string Reason { get; set; } = string.Empty;
    [StringLength(1000)] public string? DecisionComment { get; set; }
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Required, Column(TypeName = "nvarchar(max)")] public string SourceSnapshotJson { get; set; } = "{}";
    [Required, StringLength(64)] public string SourceSnapshotHash { get; set; } = string.Empty;
    [Required, Column(TypeName = "nvarchar(max)")] public string ReadinessSnapshotJson { get; set; } = "[]";
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public Contract Contract { get; set; } = null!;
    public Project Project { get; set; } = null!;
    public WorkflowDefinition WorkflowDefinition { get; set; } = null!;
    public WorkflowInstance? WorkflowInstance { get; set; }
    public ProjectHandoverItem? ProjectHandoverItem { get; set; }
    public ProjectDefectLiabilityCase? ProjectDefectLiabilityCase { get; set; }
    public ProjectFinalAccount? ProjectFinalAccount { get; set; }
    public ProjectPaymentCertificate? ProjectPaymentCertificate { get; set; }
    public PerformanceBondRequest? PerformanceBondRequest { get; set; }
    public ICollection<ProcurementWorksCloseoutEvidence> Evidence { get; set; } =
        new List<ProcurementWorksCloseoutEvidence>();
}

[Table("ProcurementWorksCloseoutEvidence")]
public sealed class ProcurementWorksCloseoutEvidence : TenantEntity
{
    public Guid ActionId { get; set; }
    [Required, StringLength(200)] public string RequirementKey { get; set; } = string.Empty;
    [Required, StringLength(300)] public string RequirementLabel { get; set; } = string.Empty;
    public ProcurementContractActivationEvidenceKind ReferenceKind { get; set; }
    public Guid? WorkflowEvidenceDocumentId { get; set; }
    public Guid? FileUploadRecordId { get; set; }
    [Required, StringLength(1000)] public string EvidenceReference { get; set; } = string.Empty;
    [Required, StringLength(64)] public string EvidenceHash { get; set; } = string.Empty;

    public ProcurementWorksCloseoutAction Action { get; set; } = null!;
    public WorkflowEvidenceDocument? WorkflowEvidenceDocument { get; set; }
    public FileUploadRecord? FileUploadRecord { get; set; }
}
