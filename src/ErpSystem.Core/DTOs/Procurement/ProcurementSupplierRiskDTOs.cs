using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Procurement;

public sealed class ProcurementSupplierRiskSummaryDto
{
    public int SupplierCount { get; set; }
    public int AssessedSupplierCount { get; set; }
    public int CurrentAssessmentCount { get; set; }
    public int OverdueAssessmentCount { get; set; }
    public int OpenAlertCount { get; set; }
    public int EscalatedAlertCount { get; set; }
    public int AwardBlockedSupplierCount { get; set; }
    public bool PolicyAvailable { get; set; }
    public string? PolicyProfileCode { get; set; }
    public int? PolicyProfileVersion { get; set; }
    public int? ExposureWindowMonths { get; set; }
    public decimal? ConcentrationLimitPercent { get; set; }
    public decimal? MinimumScore { get; set; }
    public ProcurementSupplierRiskEligibilityAction? EligibilityAction { get; set; }
    public string? PolicyReleaseGate { get; set; }
}

public sealed class ProcurementSupplierRiskSearchRequest
{
    public string? Search { get; set; }
    public Guid? BusinessPartnerId { get; set; }
    public string? RiskBand { get; set; }
    public bool? HasOpenAlerts { get; set; }
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
}

public sealed class ProcurementSupplierRiskPageDto
{
    public IReadOnlyList<ProcurementSupplierRiskListItemDto> Items { get; set; } =
        Array.Empty<ProcurementSupplierRiskListItemDto>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}

public class ProcurementSupplierRiskListItemDto
{
    public Guid Id { get; set; }
    public string AssessmentReference { get; set; } = string.Empty;
    public int AssessmentSequence { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public string PartnerCode { get; set; } = string.Empty;
    public string PartnerName { get; set; } = string.Empty;
    public DateTime AssessedAtUtc { get; set; }
    public DateTime NextReviewDueAtUtc { get; set; }
    public decimal? RiskScore { get; set; }
    public string? RiskBand { get; set; }
    public decimal MaximumSpendSharePercent { get; set; }
    public int SingleSourceCategoryCount { get; set; }
    public bool DataComplete { get; set; }
    public bool IsCurrent { get; set; }
    public int OpenAlertCount { get; set; }
    public int EscalatedAlertCount { get; set; }
    public bool AwardBlocked { get; set; }
    public string IntegrityHash { get; set; } = string.Empty;
}

public sealed class ProcurementSupplierRiskAssessmentDto : ProcurementSupplierRiskListItemDto
{
    public DateTime PeriodStartUtc { get; set; }
    public DateTime PeriodEndUtc { get; set; }
    public Guid PolicyDecisionId { get; set; }
    public Guid PolicyProfileId { get; set; }
    public string PolicyProfileCode { get; set; } = string.Empty;
    public int PolicyProfileVersion { get; set; }
    public string PolicyValueHash { get; set; } = string.Empty;
    public int ExposureWindowMonths { get; set; }
    public decimal MinimumScore { get; set; }
    public decimal ConcentrationLimitPercent { get; set; }
    public ProcurementSupplierRiskEligibilityAction EligibilityAction { get; set; }
    public bool MinimumScoreBreached { get; set; }
    public bool ConcentrationBreached { get; set; }
    public bool SingleSourceDependency { get; set; }
    public string EligibilityDecisionHash { get; set; } = string.Empty;
    public string? SourceType { get; set; }
    public Guid? SourceId { get; set; }
    public string? SourceReference { get; set; }
    public Guid AssessedByUserId { get; set; }
    public string AssessedByName { get; set; } = string.Empty;
    public IReadOnlyList<ProcurementSupplierRiskDimensionDto> Dimensions { get; set; } =
        Array.Empty<ProcurementSupplierRiskDimensionDto>();
    public IReadOnlyList<ProcurementSupplierSpendExposureDto> SpendExposure { get; set; } =
        Array.Empty<ProcurementSupplierSpendExposureDto>();
    public IReadOnlyList<ProcurementSupplierCategoryExposureDto> CategoryExposure { get; set; } =
        Array.Empty<ProcurementSupplierCategoryExposureDto>();
    public IReadOnlyList<ProcurementSupplierRiskFindingDto> Findings { get; set; } =
        Array.Empty<ProcurementSupplierRiskFindingDto>();
    public IReadOnlyList<ProcurementSupplierRiskAlertDto> Alerts { get; set; } =
        Array.Empty<ProcurementSupplierRiskAlertDto>();
    public IReadOnlyList<string> DecisionKeys { get; set; } =
        Enumerable.Range(1, 14).Select(value => $"DEC-{value:000}").ToArray();
}

public sealed class ProcurementSupplierRiskDimensionDto
{
    public string Dimension { get; set; } = string.Empty;
    public decimal WeightPercent { get; set; }
    public decimal? Score { get; set; }
    public decimal? WeightedScore { get; set; }
    public string Source { get; set; } = string.Empty;
    public string? MissingReason { get; set; }
}

public sealed class ProcurementSupplierSpendExposureDto
{
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal SupplierAmount { get; set; }
    public decimal TenantAmount { get; set; }
    public decimal SpendSharePercent { get; set; }
    public int SupplierOrderCount { get; set; }
    public int TenantOrderCount { get; set; }
}

public sealed class ProcurementSupplierCategoryExposureDto
{
    public Guid? CategoryId { get; set; }
    public string CategoryCode { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal SupplierAmount { get; set; }
    public decimal CategoryAmount { get; set; }
    public decimal SpendSharePercent { get; set; }
    public int DistinctSupplierCount { get; set; }
    public bool IsSingleSource { get; set; }
}

public sealed class ProcurementSupplierRiskFindingDto
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool Breach { get; set; }
    public bool DataGap { get; set; }
}

public sealed class ProcurementSupplierRiskAlertDto
{
    public Guid Id { get; set; }
    public Guid AssessmentId { get; set; }
    public ProcurementSupplierRiskAlertType AlertType { get; set; }
    public ProcurementSupplierRiskAlertStatus Status { get; set; }
    public string RuleCode { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime OpenedAtUtc { get; set; }
    public Guid? WorkflowDefinitionId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? EscalatedById { get; set; }
    public DateTime? EscalatedAtUtc { get; set; }
    public string? EscalationReason { get; set; }
    public Guid? ResolvedById { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public string? ResolutionReason { get; set; }
    public string RowVersion { get; set; } = string.Empty;
    public string IntegrityHash { get; set; } = string.Empty;
}

public sealed class ProcurementSupplierRiskCurrentStateDto
{
    public Guid BusinessPartnerId { get; set; }
    public string? PartnerCode { get; set; }
    public string? PartnerName { get; set; }
    public bool PolicyAvailable { get; set; }
    public string? PolicyReleaseGate { get; set; }
    public ProcurementSupplierRiskAssessmentDto? Assessment { get; set; }
    public bool Current { get; set; }
    public bool AwardBlocked { get; set; }
    public IReadOnlyList<string> AwardBlockReasons { get; set; } = Array.Empty<string>();
}

public sealed class EvaluateProcurementSupplierRiskRequest
{
    public Guid BusinessPartnerId { get; set; }
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [StringLength(100)] public string? SourceType { get; set; }
    public Guid? SourceId { get; set; }
    [StringLength(100)] public string? SourceReference { get; set; }
}

public sealed class EscalateProcurementSupplierRiskAlertRequest
{
    public Guid WorkflowDefinitionId { get; set; }
    [Required, StringLength(1000)] public string Reason { get; set; } = string.Empty;
    [Required] public string RowVersion { get; set; } = string.Empty;
    [MinLength(1)] public List<ProcurementControlEventEvidenceReference> Evidence { get; set; } = new();
}

public sealed class ResolveProcurementSupplierRiskAlertRequest
{
    [Required, StringLength(1000)] public string Reason { get; set; } = string.Empty;
    [Required] public string RowVersion { get; set; } = string.Empty;
    [MinLength(1)] public List<ProcurementControlEventEvidenceReference> Evidence { get; set; } = new();
}

public sealed class ProcurementSupplierRiskSupplierOptionDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public sealed class ProcurementSupplierRiskWorkflowOptionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Version { get; set; }
}
