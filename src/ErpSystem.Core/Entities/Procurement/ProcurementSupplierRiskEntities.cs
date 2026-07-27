using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Procurement;

[Table("ProcurementSupplierRiskAssessments")]
public sealed class ProcurementSupplierRiskAssessment : TenantEntity
{
    [Required, StringLength(50)] public string AssessmentReference { get; set; } = string.Empty;
    public int AssessmentSequence { get; set; } = 1;
    public Guid BusinessPartnerId { get; set; }
    public DateTime AssessedAtUtc { get; set; }
    public DateTime PeriodStartUtc { get; set; }
    public DateTime PeriodEndUtc { get; set; }
    public DateTime NextReviewDueAtUtc { get; set; }

    public Guid PolicyDecisionId { get; set; }
    public Guid PolicyProfileId { get; set; }
    [Required, StringLength(50)] public string PolicyProfileCode { get; set; } = string.Empty;
    public int PolicyProfileVersion { get; set; }
    [Required, Column(TypeName = "nvarchar(max)")] public string PolicySnapshotJson { get; set; } = "{}";
    [Required, StringLength(64)] public string PolicyValueHash { get; set; } = string.Empty;

    public int ExposureWindowMonths { get; set; }
    [Column(TypeName = "decimal(5,2)")] public decimal MinimumScore { get; set; }
    [Column(TypeName = "decimal(5,2)")] public decimal ConcentrationLimitPercent { get; set; }
    [Column(TypeName = "decimal(5,2)")] public decimal? RiskScore { get; set; }
    [StringLength(50)] public string? RiskBand { get; set; }
    public ProcurementSupplierRiskEligibilityAction EligibilityAction { get; set; }
    public bool DataComplete { get; set; }
    public bool MinimumScoreBreached { get; set; }
    public bool ConcentrationBreached { get; set; }
    public bool SingleSourceDependency { get; set; }
    [Column(TypeName = "decimal(5,2)")] public decimal MaximumSpendSharePercent { get; set; }
    public int SingleSourceCategoryCount { get; set; }

    [Required, Column(TypeName = "nvarchar(max)")] public string DimensionScoresJson { get; set; } = "[]";
    [Required, Column(TypeName = "nvarchar(max)")] public string SpendExposureJson { get; set; } = "[]";
    [Required, Column(TypeName = "nvarchar(max)")] public string CategoryExposureJson { get; set; } = "[]";
    [Required, Column(TypeName = "nvarchar(max)")] public string EligibilitySnapshotJson { get; set; } = "{}";
    [Required, StringLength(64)] public string EligibilityDecisionHash { get; set; } = string.Empty;
    [Required, Column(TypeName = "nvarchar(max)")] public string FindingsJson { get; set; } = "[]";

    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [StringLength(100)] public string? SourceType { get; set; }
    public Guid? SourceId { get; set; }
    [StringLength(100)] public string? SourceReference { get; set; }
    public Guid AssessedByUserId { get; set; }
    [Required, StringLength(200)] public string AssessedByName { get; set; } = string.Empty;
    [Required, Column(TypeName = "nvarchar(max)")] public string SnapshotJson { get; set; } = "{}";
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public BusinessPartner BusinessPartner { get; set; } = null!;
    public ProcurementConfigurationDecision PolicyDecision { get; set; } = null!;
    public ProcurementConfigurationProfile PolicyProfile { get; set; } = null!;
    public ICollection<ProcurementSupplierRiskAlert> Alerts { get; set; } =
        new List<ProcurementSupplierRiskAlert>();
}

[Table("ProcurementSupplierRiskAlerts")]
public sealed class ProcurementSupplierRiskAlert : TenantEntity
{
    public Guid AssessmentId { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public ProcurementSupplierRiskAlertType AlertType { get; set; }
    public ProcurementSupplierRiskAlertStatus Status { get; set; } =
        ProcurementSupplierRiskAlertStatus.Open;
    [Required, StringLength(100)] public string RuleCode { get; set; } = string.Empty;
    [Required, StringLength(50)] public string Severity { get; set; } = string.Empty;
    [Required, StringLength(1000)] public string Message { get; set; } = string.Empty;
    public DateTime OpenedAtUtc { get; set; }

    public Guid? WorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? EscalatedById { get; set; }
    public DateTime? EscalatedAtUtc { get; set; }
    [StringLength(1000)] public string? EscalationReason { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string? EscalationEvidenceJson { get; set; }
    public Guid? ResolvedById { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    [StringLength(1000)] public string? ResolutionReason { get; set; }
    [Column(TypeName = "nvarchar(max)")] public string? ResolutionEvidenceJson { get; set; }

    [Required, StringLength(100)] public string CreationCorrelationId { get; set; } = string.Empty;
    [Required, StringLength(100)] public string LastOperationCorrelationId { get; set; } = string.Empty;
    [Required, StringLength(50)] public string LastOperation { get; set; } = "Opened";
    [Required, Column(TypeName = "nvarchar(max)")] public string SnapshotJson { get; set; } = "{}";
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ProcurementSupplierRiskAssessment Assessment { get; set; } = null!;
    public BusinessPartner BusinessPartner { get; set; } = null!;
    public WorkflowDefinition? WorkflowDefinition { get; set; }
    public WorkflowInstance? WorkflowInstance { get; set; }
}
