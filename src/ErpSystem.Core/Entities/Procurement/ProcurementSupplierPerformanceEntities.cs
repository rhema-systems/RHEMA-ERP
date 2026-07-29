using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Procurement;

[Table("ProcurementSupplierPerformanceScorecards")]
public sealed class ProcurementSupplierPerformanceScorecard : TenantEntity
{
    [Required, StringLength(50)] public string ScorecardReference { get; set; } = string.Empty;
    public int ScorecardSequence { get; set; } = 1;
    public Guid BusinessPartnerId { get; set; }
    public DateTime CalculatedAtUtc { get; set; }
    public DateTime PeriodStartUtc { get; set; }
    public DateTime PeriodEndUtc { get; set; }
    public DateTime NextReviewDueAtUtc { get; set; }

    public Guid PolicyDecisionId { get; set; }
    public Guid PolicyProfileId { get; set; }
    [Required, StringLength(50)] public string PolicyProfileCode { get; set; } = string.Empty;
    public int PolicyProfileVersion { get; set; }
    [Required, Column(TypeName = "nvarchar(max)")] public string PolicySnapshotJson { get; set; } = "{}";
    [Required, StringLength(64)] public string PolicyValueHash { get; set; } = string.Empty;

    public int PerformanceWindowMonths { get; set; }
    [Column(TypeName = "decimal(5,2)")] public decimal MinimumScore { get; set; }
    [Column(TypeName = "decimal(5,2)")] public decimal MinimumDataCoveragePercent { get; set; }
    [Column(TypeName = "decimal(10,2)")] public decimal ResponseTargetHours { get; set; }
    public ProcurementSupplierRiskEligibilityAction EligibilityAction { get; set; }
    public ProcurementSupplierPerformanceDataStatus DataStatus { get; set; }
    [Column(TypeName = "decimal(5,2)")] public decimal DataCoveragePercent { get; set; }
    [Column(TypeName = "decimal(5,2)")] public decimal? OverallScore { get; set; }
    [StringLength(50)] public string? PerformanceBand { get; set; }
    public bool MinimumScoreBreached { get; set; }

    [Column(TypeName = "decimal(5,2)")] public decimal? DeliveryTimelinessScore { get; set; }
    [Column(TypeName = "decimal(5,2)")] public decimal? GrnQualityScore { get; set; }
    [Column(TypeName = "decimal(5,2)")] public decimal? RejectionRateScore { get; set; }
    [Column(TypeName = "decimal(5,2)")] public decimal? PriceCompetitivenessScore { get; set; }
    [Column(TypeName = "decimal(5,2)")] public decimal? ResponsivenessScore { get; set; }
    [Column(TypeName = "decimal(5,2)")] public decimal? ComplaintResolutionScore { get; set; }
    [Column(TypeName = "decimal(5,2)")] public decimal? ContractCompletionScore { get; set; }

    public int PurchaseOrderCount { get; set; }
    public int ReceiptCount { get; set; }
    public int ReceiptLineCount { get; set; }
    public int PriceComparisonCount { get; set; }
    public int ResponseObservationCount { get; set; }
    public int ComplaintCount { get; set; }
    public int ContractCount { get; set; }

    [Required, Column(TypeName = "nvarchar(max)")] public string MeasureResultsJson { get; set; } = "[]";
    [Required, Column(TypeName = "nvarchar(max)")] public string SourceSnapshotJson { get; set; } = "{}";
    [Required, StringLength(64)] public string SourceSnapshotHash { get; set; } = string.Empty;
    [Required, Column(TypeName = "nvarchar(max)")] public string SupplierControlSnapshotJson { get; set; } = "{}";
    [Required, StringLength(64)] public string SupplierEligibilityDecisionHash { get; set; } = string.Empty;
    public Guid? RiskAssessmentId { get; set; }
    [StringLength(64)] public string? RiskAssessmentIntegrityHash { get; set; }
    [Required, Column(TypeName = "nvarchar(max)")] public string FindingsJson { get; set; } = "[]";

    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [Required, StringLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [StringLength(100)] public string? SourceType { get; set; }
    public Guid? SourceId { get; set; }
    [StringLength(100)] public string? SourceReference { get; set; }
    public Guid CalculatedByUserId { get; set; }
    [Required, StringLength(200)] public string CalculatedByName { get; set; } = string.Empty;
    [Required, Column(TypeName = "nvarchar(max)")] public string SnapshotJson { get; set; } = "{}";
    [Required, StringLength(64)] public string IntegrityHash { get; set; } = string.Empty;

    public BusinessPartner BusinessPartner { get; set; } = null!;
    public ProcurementConfigurationDecision PolicyDecision { get; set; } = null!;
    public ProcurementConfigurationProfile PolicyProfile { get; set; } = null!;
    public ProcurementSupplierRiskAssessment? RiskAssessment { get; set; }
}
