using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Procurement;

// ============================================================================
// SUPPLIER PERFORMANCE METRIC DTOs
// ============================================================================

public class SupplierPerformanceMetricDto
{
    public Guid Id { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public string? PartnerName { get; set; }
    public string? PartnerCode { get; set; }
    public string MetricPeriod { get; set; } = string.Empty;
    public int Year { get; set; }
    public int? Month { get; set; }
    public int? Quarter { get; set; }

    // Delivery Performance
    public int TotalOrders { get; set; }
    public int OnTimeDeliveries { get; set; }
    public int LateDeliveries { get; set; }
    public decimal OnTimeDeliveryRate { get; set; }
    public decimal AverageDeliveryDelayDays { get; set; }

    // Quality Metrics
    public int TotalItemsReceived { get; set; }
    public int DefectiveItems { get; set; }
    public decimal QualityAcceptanceRate { get; set; }
    public decimal DefectRate { get; set; }

    // Cost Competitiveness
    public decimal TotalPurchaseValue { get; set; }
    public decimal CostCompetitivenessScore { get; set; }

    // Service
    public decimal CustomerServiceRating { get; set; }
    public int ComplaintsReceived { get; set; }
    public int ComplaintsResolved { get; set; }

    // Compliance
    public int ContractViolations { get; set; }
    public decimal ComplianceScore { get; set; }

    // Innovation
    public int InnovationSuggestions { get; set; }
    public decimal EstimatedCostSavings { get; set; }

    // Overall
    public decimal OverallPerformanceScore { get; set; }
    public string? PerformanceGrade { get; set; }

    public DateTime CalculatedAt { get; set; }
    public string? Notes { get; set; }
}

public class CreateSupplierPerformanceMetricDto
{
    [Required]
    public Guid BusinessPartnerId { get; set; }

    [Required]
    public string MetricPeriod { get; set; } = "Monthly";

    [Required]
    public int Year { get; set; }

    public int? Month { get; set; }
    public int? Quarter { get; set; }

    public string? Notes { get; set; }
}

public class PerformanceTrendDto
{
    public string Period { get; set; } = string.Empty;
    public decimal OverallScore { get; set; }
    public decimal DeliveryScore { get; set; }
    public decimal QualityScore { get; set; }
    public decimal CostScore { get; set; }
    public decimal ServiceScore { get; set; }
}

// ============================================================================
// QUALITY INCIDENT DTOs
// ============================================================================

public class QualityIncidentDto
{
    public Guid Id { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public string? PartnerName { get; set; }
    public Guid? PurchaseOrderId { get; set; }
    public string? PurchaseOrderNumber { get; set; }
    public string IncidentNumber { get; set; } = string.Empty;
    public DateTime IncidentDate { get; set; }
    public string IncidentType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int QuantityAffected { get; set; }
    public decimal? FinancialImpact { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? ReportedDate { get; set; }
    public string? ReportedByName { get; set; }
    public DateTime? ResolvedDate { get; set; }
    public string? Resolution { get; set; }
    public string? RootCause { get; set; }
    public string? CorrectiveAction { get; set; }
    public bool RequiresSupplierResponse { get; set; }
    public DateTime? SupplierResponseDate { get; set; }
    public string? SupplierResponse { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateQualityIncidentDto
{
    [Required]
    public Guid BusinessPartnerId { get; set; }

    public Guid? PurchaseOrderId { get; set; }

    [Required]
    public DateTime IncidentDate { get; set; }

    [Required]
    [MaxLength(50)]
    public string IncidentType { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Severity { get; set; } = "Medium";

    [Required]
    public string Description { get; set; } = string.Empty;

    public int QuantityAffected { get; set; }
    public decimal? FinancialImpact { get; set; }
    public bool RequiresSupplierResponse { get; set; } = true;
    public string? Notes { get; set; }
}

public class UpdateQualityIncidentDto
{
    public string? Status { get; set; }
    public string? Resolution { get; set; }
    public string? RootCause { get; set; }
    public string? CorrectiveAction { get; set; }
    public string? PreventiveAction { get; set; }
}

public class SupplierResponseDto
{
    [Required]
    public string Response { get; set; } = string.Empty;
}

// ============================================================================
// PERFORMANCE REVIEW DTOs
// ============================================================================

public class PerformanceReviewDto
{
    public Guid Id { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public string? PartnerName { get; set; }
    public string ReviewNumber { get; set; } = string.Empty;
    public DateTime ReviewDate { get; set; }
    public string ReviewPeriod { get; set; } = string.Empty;
    public int ReviewYear { get; set; }
    public int? ReviewMonth { get; set; }
    public int? ReviewQuarter { get; set; }
    public DateTime PeriodStartDate { get; set; }
    public DateTime PeriodEndDate { get; set; }
    public string? ReviewedByName { get; set; }

    // Scores
    public decimal DeliveryPerformanceScore { get; set; }
    public decimal QualityScore { get; set; }
    public decimal CostCompetitivenessScore { get; set; }
    public decimal CustomerServiceScore { get; set; }
    public decimal ComplianceScore { get; set; }
    public decimal InnovationScore { get; set; }
    public decimal OverallScore { get; set; }
    public string? OverallGrade { get; set; }

    // Content
    public string Strengths { get; set; } = string.Empty;
    public string AreasForImprovement { get; set; } = string.Empty;
    public string? Recommendations { get; set; }
    public string? ActionItems { get; set; }

    public string Status { get; set; } = string.Empty;
    public DateTime? SubmittedDate { get; set; }
    public DateTime? AcknowledgedDate { get; set; }
    public string? SupplierComments { get; set; }
    public DateTime? SupplierCommentsDate { get; set; }
    public bool RequiresFollowUp { get; set; }
    public DateTime? FollowUpDate { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreatePerformanceReviewDto
{
    [Required]
    public Guid BusinessPartnerId { get; set; }

    [Required]
    public DateTime ReviewDate { get; set; }

    [Required]
    public string ReviewPeriod { get; set; } = string.Empty;

    [Required]
    public DateTime PeriodStartDate { get; set; }

    [Required]
    public DateTime PeriodEndDate { get; set; }

    [Required]
    [Range(0, 5)]
    public decimal DeliveryPerformanceScore { get; set; }

    [Required]
    [Range(0, 5)]
    public decimal QualityScore { get; set; }

    [Required]
    [Range(0, 5)]
    public decimal CostCompetitivenessScore { get; set; }

    [Required]
    [Range(0, 5)]
    public decimal CustomerServiceScore { get; set; }

    [Required]
    [Range(0, 5)]
    public decimal ComplianceScore { get; set; }

    [Required]
    [Range(0, 5)]
    public decimal InnovationScore { get; set; }

    [Required]
    public string Strengths { get; set; } = string.Empty;

    [Required]
    public string AreasForImprovement { get; set; } = string.Empty;

    public string? Recommendations { get; set; }
    public string? ActionItems { get; set; }
    public bool RequiresFollowUp { get; set; }
    public DateTime? FollowUpDate { get; set; }
    public string? Notes { get; set; }
}

public class PerformanceReportCardDto
{
    public Guid BusinessPartnerId { get; set; }
    public string PartnerName { get; set; } = string.Empty;
    public string PartnerCode { get; set; } = string.Empty;
    public string PartnerType { get; set; } = string.Empty;
    public string ReportPeriod { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }

    // Current Period Metrics
    public SupplierPerformanceMetricDto? CurrentMetrics { get; set; }

    // Historical Trends
    public List<PerformanceTrendDto> Trends { get; set; } = new();

    // Recent Quality Incidents
    public List<QualityIncidentDto> RecentIncidents { get; set; } = new();

    // Latest Review
    public PerformanceReviewDto? LatestReview { get; set; }

    // Summary Statistics
    public int TotalOrdersAllTime { get; set; }
    public decimal AverageOnTimeDeliveryRate { get; set; }
    public decimal AverageQualityRate { get; set; }
    public int TotalIncidentsAllTime { get; set; }
    public int OpenIncidents { get; set; }
}

