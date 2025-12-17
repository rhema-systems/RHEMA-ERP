using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Procurement;

/// <summary>
/// Tender evaluation DTO
/// </summary>
public class TenderEvaluationDto
{
    public Guid Id { get; set; }
    public Guid TenderBidId { get; set; }
    public Guid TenderEvaluatorId { get; set; }
    public string EvaluatorName { get; set; } = string.Empty;
    public string EvaluatorRole { get; set; } = "Evaluator";
    public DateTime EvaluationDate { get; set; }
    public string Status { get; set; } = "Draft";

    // Tender and Business Partner Information
    public Guid? TenderId { get; set; }
    public string TenderNumber { get; set; } = string.Empty;
    public string TenderTitle { get; set; } = string.Empty;
    public string BusinessPartnerName { get; set; } = string.Empty;
    public string BidNumber { get; set; } = string.Empty;

    // Scores
    public decimal? PriceScore { get; set; }
    public decimal? QualityScore { get; set; }
    public decimal? DeliveryScore { get; set; }
    public decimal? ExperienceScore { get; set; }
    public decimal? TechnicalScore { get; set; }
    public decimal? ComplianceScore { get; set; }
    public decimal? TotalScore { get; set; }

    public string? EvaluationCriteriaJson { get; set; }
    public string? TechnicalComments { get; set; }
    public string? CommercialComments { get; set; }
    public string? OverallComments { get; set; }
    public bool IsRecommended { get; set; }
    public string? Recommendation { get; set; }
    public DateTime? SubmittedDate { get; set; }
}

/// <summary>
/// Create evaluation DTO
/// </summary>
public class CreateEvaluationDto
{
    [Required]
    public Guid TenderBidId { get; set; }

    [Range(0, 100)]
    public decimal? PriceScore { get; set; }

    [Range(0, 100)]
    public decimal? QualityScore { get; set; }

    [Range(0, 100)]
    public decimal? DeliveryScore { get; set; }

    [Range(0, 100)]
    public decimal? ExperienceScore { get; set; }

    [Range(0, 100)]
    public decimal? TechnicalScore { get; set; }

    [Range(0, 100)]
    public decimal? ComplianceScore { get; set; }

    public string? EvaluationCriteriaJson { get; set; }
    public string? TechnicalComments { get; set; }
    public string? CommercialComments { get; set; }
    public string? OverallComments { get; set; }
    public bool IsRecommended { get; set; }
    public string? Recommendation { get; set; }
}

/// <summary>
/// Update evaluation DTO
/// </summary>
public class UpdateEvaluationDto
{
    [Range(0, 100)]
    public decimal? PriceScore { get; set; }

    [Range(0, 100)]
    public decimal? QualityScore { get; set; }

    [Range(0, 100)]
    public decimal? DeliveryScore { get; set; }

    [Range(0, 100)]
    public decimal? ExperienceScore { get; set; }

    [Range(0, 100)]
    public decimal? TechnicalScore { get; set; }

    [Range(0, 100)]
    public decimal? ComplianceScore { get; set; }

    public string? EvaluationCriteriaJson { get; set; }
    public string? TechnicalComments { get; set; }
    public string? CommercialComments { get; set; }
    public string? OverallComments { get; set; }
    public bool IsRecommended { get; set; }
    public string? Recommendation { get; set; }
}

/// <summary>
/// Submit evaluation DTO
/// </summary>
public class SubmitEvaluationDto
{
    [Required]
    public bool ConfirmSubmission { get; set; }
}

/// <summary>
/// Evaluation scorecard DTO for display
/// </summary>
public class EvaluationScorecardDto
{
    public Guid TenderBidId { get; set; }
    public string BidNumber { get; set; } = string.Empty;
    public string BusinessPartnerName { get; set; } = string.Empty;
    public decimal TotalBidAmount { get; set; }
    public string Currency { get; set; } = "USD";
    public string BidStatus { get; set; } = string.Empty;
    public int? Rank { get; set; }
    public bool IsRecommended => RecommendationCount > 0;

    // Individual evaluator scores
    public List<TenderEvaluationDto> Evaluations { get; set; } = new();

    // Consolidated scores
    public decimal? AveragePriceScore { get; set; }
    public decimal? AverageQualityScore { get; set; }
    public decimal? AverageDeliveryScore { get; set; }
    public decimal? AverageExperienceScore { get; set; }
    public decimal? AverageTechnicalScore { get; set; }
    public decimal? AverageComplianceScore { get; set; }
    public decimal? AverageTotalScore { get; set; }

    public int RecommendationCount { get; set; }
    public int TotalEvaluators { get; set; }
}

/// <summary>
/// Consolidated evaluation DTO for all bids
/// </summary>
public class ConsolidatedEvaluationDto
{
    public Guid TenderId { get; set; }
    public string TenderNumber { get; set; } = string.Empty;
    public string TenderTitle { get; set; } = string.Empty;
    public List<EvaluationScorecardDto> BidScorecards { get; set; } = new();
    public DateTime GeneratedDate { get; set; }
    public string? GeneratedByName { get; set; }
}



/// <summary>
/// Evaluation report DTO
/// </summary>
public class EvaluationReportDto
{
    public Guid TenderId { get; set; }
    public string TenderNumber { get; set; } = string.Empty;
    public string TenderTitle { get; set; } = string.Empty;
    public string TenderType { get; set; } = "RFQ";
    public DateTime? PublishDate { get; set; }
    public DateTime? SubmissionDeadline { get; set; }
    public decimal? EstimatedValue { get; set; }

    // Evaluation Criteria
    public decimal PriceWeightage { get; set; }
    public decimal QualityWeightage { get; set; }
    public decimal DeliveryWeightage { get; set; }
    public decimal ExperienceWeightage { get; set; }

    // Statistics
    public int TotalBidsReceived { get; set; }
    public int CompliantBids { get; set; }
    public int NonCompliantBids { get; set; }
    public int EvaluatedBids { get; set; }

    // Bid Evaluations
    public List<BidEvaluationSummaryDto> BidEvaluations { get; set; } = new();

    // Recommendation
    public Guid? RecommendedBidId { get; set; }
    public string? RecommendedBidNumber { get; set; }
    public string? RecommendedBusinessPartnerName { get; set; }
    public decimal? RecommendedBidAmount { get; set; }
    public string? RecommendationJustification { get; set; }

    public DateTime GeneratedDate { get; set; }
    public string? GeneratedByName { get; set; }
}

/// <summary>
/// Bid evaluation summary for report
/// </summary>
public class BidEvaluationSummaryDto
{
    public Guid BidId { get; set; }
    public string BidNumber { get; set; } = string.Empty;
    public string BusinessPartnerName { get; set; } = string.Empty;
    public decimal TotalBidAmount { get; set; }
    public string BidStatus { get; set; } = string.Empty;
    public bool IsCompliant { get; set; }
    public string? NonComplianceReasons { get; set; }

    // Weighted Scores
    public decimal? WeightedPriceScore { get; set; }
    public decimal? WeightedQualityScore { get; set; }
    public decimal? WeightedDeliveryScore { get; set; }
    public decimal? WeightedExperienceScore { get; set; }
    public decimal? FinalScore { get; set; }

    public int Rank { get; set; }
    public int RecommendationCount { get; set; }
    public bool IsRecommended { get; set; }
}

