using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Procurement;

public sealed class ProcurementSupplierPerformanceSummaryDto
{
    public int SupplierCount { get; set; }
    public int ScoredSupplierCount { get; set; }
    public int CurrentScorecardCount { get; set; }
    public int BelowMinimumCount { get; set; }
    public int InsufficientCoverageCount { get; set; }
    public bool PolicyAvailable { get; set; }
    public string? PolicyProfileCode { get; set; }
    public int? PolicyProfileVersion { get; set; }
    public int? PerformanceWindowMonths { get; set; }
    public decimal? MinimumScore { get; set; }
    public decimal? MinimumDataCoveragePercent { get; set; }
    public decimal? ResponseTargetHours { get; set; }
    public ProcurementSupplierRiskEligibilityAction? EligibilityAction { get; set; }
    public string? PolicyReleaseGate { get; set; }
}

public sealed class ProcurementSupplierPerformanceSearchRequest
{
    public string? Search { get; set; }
    public Guid? BusinessPartnerId { get; set; }
    public string? PerformanceBand { get; set; }
    public ProcurementSupplierPerformanceDataStatus? DataStatus { get; set; }
    public bool? BelowMinimum { get; set; }
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
}

public sealed class ProcurementSupplierPerformancePageDto
{
    public IReadOnlyList<ProcurementSupplierPerformanceListItemDto> Items { get; set; } =
        Array.Empty<ProcurementSupplierPerformanceListItemDto>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}

public class ProcurementSupplierPerformanceListItemDto
{
    public Guid Id { get; set; }
    public string ScorecardReference { get; set; } = string.Empty;
    public int ScorecardSequence { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public string PartnerCode { get; set; } = string.Empty;
    public string PartnerName { get; set; } = string.Empty;
    public DateTime CalculatedAtUtc { get; set; }
    public DateTime PeriodStartUtc { get; set; }
    public DateTime PeriodEndUtc { get; set; }
    public DateTime NextReviewDueAtUtc { get; set; }
    public decimal? OverallScore { get; set; }
    public string? PerformanceBand { get; set; }
    public ProcurementSupplierPerformanceDataStatus DataStatus { get; set; }
    public decimal DataCoveragePercent { get; set; }
    public bool MinimumScoreBreached { get; set; }
    public bool IsCurrent { get; set; }
    public string IntegrityHash { get; set; } = string.Empty;
}

public sealed class ProcurementSupplierPerformanceScorecardDto :
    ProcurementSupplierPerformanceListItemDto
{
    public Guid PolicyDecisionId { get; set; }
    public Guid PolicyProfileId { get; set; }
    public string PolicyProfileCode { get; set; } = string.Empty;
    public int PolicyProfileVersion { get; set; }
    public string PolicyValueHash { get; set; } = string.Empty;
    public int PerformanceWindowMonths { get; set; }
    public decimal MinimumScore { get; set; }
    public decimal MinimumDataCoveragePercent { get; set; }
    public decimal ResponseTargetHours { get; set; }
    public ProcurementSupplierRiskEligibilityAction EligibilityAction { get; set; }
    public string SourceSnapshotHash { get; set; } = string.Empty;
    public string SupplierEligibilityDecisionHash { get; set; } = string.Empty;
    public Guid? RiskAssessmentId { get; set; }
    public string? RiskAssessmentIntegrityHash { get; set; }
    public string? SourceType { get; set; }
    public Guid? SourceId { get; set; }
    public string? SourceReference { get; set; }
    public Guid CalculatedByUserId { get; set; }
    public string CalculatedByName { get; set; } = string.Empty;
    public IReadOnlyList<ProcurementSupplierPerformanceMeasureDto> Measures { get; set; } =
        Array.Empty<ProcurementSupplierPerformanceMeasureDto>();
    public IReadOnlyList<ProcurementSupplierPerformanceFindingDto> Findings { get; set; } =
        Array.Empty<ProcurementSupplierPerformanceFindingDto>();
    public IReadOnlyList<string> DecisionKeys { get; set; } =
        Enumerable.Range(1, 14).Select(value => $"DEC-{value:000}").ToArray();
}

public sealed class ProcurementSupplierPerformanceMeasureDto
{
    public ProcurementSupplierPerformanceMetricKey Metric { get; set; }
    public decimal WeightPercent { get; set; }
    public decimal? Score { get; set; }
    public decimal? AppliedWeightPercent { get; set; }
    public decimal? WeightedContribution { get; set; }
    public int ObservationCount { get; set; }
    public decimal? Numerator { get; set; }
    public decimal? Denominator { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string? MissingReason { get; set; }
}

public sealed class ProcurementSupplierPerformanceFindingDto
{
    public string Code { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool Breach { get; set; }
    public bool DataGap { get; set; }
}

public sealed class ProcurementSupplierPerformanceCurrentStateDto
{
    public Guid BusinessPartnerId { get; set; }
    public string? PartnerCode { get; set; }
    public string? PartnerName { get; set; }
    public bool PolicyAvailable { get; set; }
    public string? PolicyReleaseGate { get; set; }
    public ProcurementSupplierPerformanceScorecardDto? Scorecard { get; set; }
    public bool Current { get; set; }
    public bool AwardBlocked { get; set; }
    public IReadOnlyList<string> AwardBlockReasons { get; set; } = Array.Empty<string>();
}

public sealed class CalculateProcurementSupplierPerformanceRequest
{
    public Guid BusinessPartnerId { get; set; }
    [Required, StringLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [StringLength(100)] public string? SourceType { get; set; }
    public Guid? SourceId { get; set; }
    [StringLength(100)] public string? SourceReference { get; set; }
}

public sealed class ProcurementSupplierPerformanceSupplierOptionDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
