using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Projects;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Services.QuantitySurvey;

namespace ErpSystem.Core.Entities.QuantitySurvey;

public enum QuantitySurveyEscalationImpactTargetType
{
    PaymentCertificate = 0,
    FinalAccount = 1
}

[Table("QuantitySurveyEscalationCalculationRuns")]
public sealed class QuantitySurveyEscalationCalculationRun : TenantEntity, IQuantitySurveyWorkflowRecord
{
    [Required, StringLength(40)] public string RunReference { get; set; } = string.Empty;
    public Guid ClientRequestId { get; set; }
    [Required, StringLength(64)] public string RequestHash { get; set; } = string.Empty;
    public Guid FormulaId { get; set; }
    public Guid FormulaKeySnapshot { get; set; }
    public int FormulaVersionSnapshot { get; set; }
    [Required, StringLength(40)] public string FormulaCodeSnapshot { get; set; } = string.Empty;
    public Guid ProjectId { get; set; }
    public Guid ContractId { get; set; }
    [Required, StringLength(50)] public string ContractNumberSnapshot { get; set; } = string.Empty;
    public DateTime BaseIndexPeriod { get; set; }
    public DateTime CurrentIndexPeriod { get; set; }
    public DateTime CalculationDate { get; set; }
    public QuantitySurveyEscalationImpactTargetType ImpactTargetType { get; set; }
    public Guid? PaymentCertificateId { get; set; }
    public Guid? FinalAccountId { get; set; }
    [Required, StringLength(120)] public string ImpactTargetReferenceSnapshot { get; set; } = string.Empty;
    [Required, StringLength(30)] public string ImpactTargetStatusSnapshot { get; set; } = string.Empty;
    [Required, StringLength(64)] public string ImpactTargetSnapshotHash { get; set; } = string.Empty;
    [Required, StringLength(3)] public string CurrencyCode { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,2)")] public decimal BaseRate { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal RevisedRate { get; set; }
    [Column(TypeName = "decimal(18,12)")] public decimal AdjustmentFactor { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal CalculatedFluctuationAmount { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal ReviewerAdjustmentAmount { get; set; }
    [StringLength(1000)] public string? ReviewerAdjustmentReason { get; set; }
    public Guid? ReviewedById { get; set; }
    public DateTime? ReviewedAt { get; set; }
    [Column(TypeName = "decimal(18,2)")] public decimal ApprovedImpactAmount { get; set; }
    [Required, StringLength(30)] public string ImpactApplicationStatus { get; set; } = "Projected";
    public Guid AuthorityRoleId { get; set; }
    [Required, StringLength(160)] public string AuthorityRoleNameSnapshot { get; set; } = string.Empty;
    public Guid ConfigurationProfileId { get; set; }
    public Guid ConfigurationDecisionId { get; set; }
    public Guid ApprovalWorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    [Required, StringLength(64)] public string SnapshotHash { get; set; } = string.Empty;
    [Required, StringLength(30)] public string Status { get; set; } = "Draft";
    [Required, StringLength(30)] public string ApprovalStatus { get; set; } = "Draft";
    public Guid PreparedById { get; set; }
    public DateTime PreparedAt { get; set; }
    public Guid? SubmittedById { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }
    [StringLength(1000)] public string? RejectionReason { get; set; }
    [Required, StringLength(100)] public string AuditAction { get; set; } = string.Empty;
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [StringLength(1000)] public string? ChangeReason { get; set; }
    [StringLength(500)] public string? ActorRoles { get; set; }
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public QuantitySurveyEscalationFormulaDefinition Formula { get; set; } = null!;
    public Project Project { get; set; } = null!;
    public Contract Contract { get; set; } = null!;
    public ProjectPaymentCertificate? PaymentCertificate { get; set; }
    public ProjectFinalAccount? FinalAccount { get; set; }
    public ApplicationRole AuthorityRole { get; set; } = null!;
    public QuantitySurveyConfigurationProfile ConfigurationProfile { get; set; } = null!;
    public QuantitySurveyConfigurationDecision ConfigurationDecision { get; set; } = null!;
    public WorkflowDefinition ApprovalWorkflowDefinition { get; set; } = null!;
    public ICollection<QuantitySurveyEscalationCalculationLine> Lines { get; set; } = new List<QuantitySurveyEscalationCalculationLine>();
}

[Table("QuantitySurveyEscalationCalculationLines")]
public sealed class QuantitySurveyEscalationCalculationLine : TenantEntity
{
    public Guid CalculationRunId { get; set; }
    public int Sequence { get; set; }
    public QuantitySurveyEscalationComponentType Component { get; set; }
    [Column(TypeName = "decimal(9,4)")] public decimal Coefficient { get; set; }
    public Guid IndexFamilyId { get; set; }
    [Required, StringLength(40)] public string IndexFamilyCodeSnapshot { get; set; } = string.Empty;
    public Guid BaseIndexValueId { get; set; }
    public Guid CurrentIndexValueId { get; set; }
    [Column(TypeName = "decimal(18,6)")] public decimal BaseIndexValue { get; set; }
    [Column(TypeName = "decimal(18,6)")] public decimal CurrentIndexValue { get; set; }
    [Column(TypeName = "decimal(18,12)")] public decimal IndexRatio { get; set; }
    [Column(TypeName = "decimal(18,12)")] public decimal WeightedContribution { get; set; }

    public QuantitySurveyEscalationCalculationRun CalculationRun { get; set; } = null!;
    public QuantitySurveyPriceIndexFamily IndexFamily { get; set; } = null!;
    public QuantitySurveyPriceIndexValue BaseIndexValueRecord { get; set; } = null!;
    public QuantitySurveyPriceIndexValue CurrentIndexValueRecord { get; set; } = null!;
}

[Table("QuantitySurveyEscalationCalculationRevisions")]
public sealed class QuantitySurveyEscalationCalculationRevision : TenantEntity
{
    public Guid CalculationRunId { get; set; }
    [Required, StringLength(100)] public string Action { get; set; } = string.Empty;
    public Guid ActorUserId { get; set; }
    [Required, StringLength(300)] public string ActorName { get; set; } = string.Empty;
    [StringLength(500)] public string? ActorRoles { get; set; }
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [StringLength(1000)] public string? Reason { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string? BeforeJson { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string? AfterJson { get; set; }

    public QuantitySurveyEscalationCalculationRun CalculationRun { get; set; } = null!;
}
