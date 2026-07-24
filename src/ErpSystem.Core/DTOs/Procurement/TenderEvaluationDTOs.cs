using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Procurement;

/// <summary>
/// Tender evaluation DTO
/// </summary>
public class TenderEvaluationDto
{
    public Guid Id { get; set; }
    public Guid TenderBidId { get; set; }

    /// <summary>
    /// Optional LOT bid ID for per-LOT evaluations
    /// </summary>
    public Guid? BidLotId { get; set; }
    public string? LotCode { get; set; }
    public string? LotTitle { get; set; }

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

    /// <summary>
    /// Optional LOT bid ID for per-LOT evaluations
    /// </summary>
    public Guid? BidLotId { get; set; }

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

    [Required, StringLength(500)]
    public string SignatureReference { get; set; } = string.Empty;

    [Required, StringLength(500)]
    public string EvidenceReference { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string IdempotencyKey { get; set; } = string.Empty;
}

/// <summary>
/// Evaluation scorecard DTO for display
/// </summary>
public class EvaluationScorecardDto
{
    public Guid TenderBidId { get; set; }

    /// <summary>
    /// Optional LOT bid ID for per-LOT scorecards
    /// </summary>
    public Guid? BidLotId { get; set; }
    public string? LotCode { get; set; }
    public string? LotTitle { get; set; }

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
/// LOT-level evaluation scorecard DTO
/// </summary>
public class LotEvaluationScorecardDto
{
    public Guid LotId { get; set; }
    public string LotCode { get; set; } = string.Empty;
    public string LotTitle { get; set; } = string.Empty;
    public decimal EstimatedValue { get; set; }
    public string? Currency { get; set; }
    public int TotalBidLots { get; set; }
    public int EvaluatedBidLots { get; set; }
    public List<EvaluationScorecardDto> BidLotScorecards { get; set; } = new();
}

/// <summary>
/// Consolidated evaluation DTO for all bids
/// </summary>
public class ConsolidatedEvaluationDto
{
    public Guid TenderId { get; set; }
    public string TenderNumber { get; set; } = string.Empty;
    public string TenderTitle { get; set; } = string.Empty;

    /// <summary>
    /// Tender-level bid scorecards (for non-LOT tenders or tender-level evaluation)
    /// </summary>
    public List<EvaluationScorecardDto> BidScorecards { get; set; } = new();

    /// <summary>
    /// LOT-level scorecards (for LOT-based tenders)
    /// </summary>
    public List<LotEvaluationScorecardDto> LotScorecards { get; set; } = new();

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
    public string? Currency { get; set; }

    // QCBS Configuration
    public bool UseQCBSEvaluation { get; set; }
    public decimal TechnicalWeight { get; set; }
    public decimal FinancialWeight { get; set; }
    public decimal MinimumTechnicalScore { get; set; }
    public decimal? LowestBidAmount { get; set; }
    public int? QualifiedBidsCount { get; set; }
    public int? DisqualifiedBidsCount { get; set; }


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

    // QCBS Scores
    public decimal? TechnicalScore { get; set; }
    public decimal? FinancialScore { get; set; }
    public decimal? CombinedScore { get; set; }
    public bool? IsQualifiedTechnically { get; set; }
    public string? DisqualificationReason { get; set; }


    public int Rank { get; set; }
    public int RecommendationCount { get; set; }
    public bool IsRecommended { get; set; }
}

/// <summary>
/// QCBS (Quality and Cost Based Selection) evaluation result for a single bid
/// </summary>
public class QCBSBidScoreDto
{
    public Guid BidId { get; set; }
    public string BidNumber { get; set; } = string.Empty;
    public Guid BusinessPartnerId { get; set; }
    public string BusinessPartnerName { get; set; } = string.Empty;
    public decimal TotalBidAmount { get; set; }
    public string Currency { get; set; } = "USD";

    /// <summary>
    /// Technical score (0-100) based on evaluation criteria
    /// </summary>
    public decimal TechnicalScore { get; set; }

    /// <summary>
    /// Financial score (0-100) calculated using lowest price formula
    /// </summary>
    public decimal FinancialScore { get; set; }

    /// <summary>
    /// Combined QCBS score = (TechnicalWeight * TechnicalScore) + (FinancialWeight * FinancialScore)
    /// </summary>
    public decimal CombinedScore { get; set; }

    /// <summary>
    /// Whether the bid meets the minimum technical score threshold
    /// </summary>
    public bool IsQualifiedTechnically { get; set; }

    /// <summary>
    /// Reason for disqualification if not qualified
    /// </summary>
    public string? DisqualificationReason { get; set; }

    /// <summary>
    /// Rank based on combined score (1 = highest)
    /// </summary>
    public int Rank { get; set; }

    /// <summary>
    /// Whether this bid is recommended for award (rank 1 and qualified)
    /// </summary>
    public bool IsRecommendedForAward { get; set; }
}

/// <summary>
/// QCBS evaluation result for a tender
/// </summary>
public class QCBSEvaluationResultDto
{
    public Guid TenderId { get; set; }
    public string TenderNumber { get; set; } = string.Empty;
    public string TenderTitle { get; set; } = string.Empty;

    /// <summary>
    /// Technical weight percentage (e.g., 80 for 80%)
    /// </summary>
    public decimal TechnicalWeight { get; set; }

    /// <summary>
    /// Financial weight percentage (e.g., 20 for 20%)
    /// </summary>
    public decimal FinancialWeight { get; set; }

    /// <summary>
    /// Minimum technical score required to qualify for financial evaluation
    /// </summary>
    public decimal MinimumTechnicalScore { get; set; }

    /// <summary>
    /// Lowest bid amount among qualified bids (used for financial score calculation)
    /// </summary>
    public decimal? LowestBidAmount { get; set; }

    /// <summary>
    /// Total number of bids evaluated
    /// </summary>
    public int TotalBids { get; set; }

    /// <summary>
    /// Number of bids that met the minimum technical score
    /// </summary>
    public int QualifiedBids { get; set; }

    /// <summary>
    /// Number of bids that did not meet the minimum technical score
    /// </summary>
    public int DisqualifiedBids { get; set; }

    /// <summary>
    /// Ranked list of bid scores
    /// </summary>
    public List<QCBSBidScoreDto> BidScores { get; set; } = new();

    /// <summary>
    /// Recommended winning bid (highest combined score among qualified bids)
    /// </summary>
    public QCBSBidScoreDto? RecommendedBid { get; set; }

    /// <summary>
    /// Date when the QCBS evaluation was calculated
    /// </summary>
    public DateTime CalculatedAt { get; set; }

    /// <summary>
    /// Name of the user who triggered the calculation
    /// </summary>
    public string? CalculatedByName { get; set; }
}
