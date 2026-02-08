using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Procurement;

/// <summary>
/// Tender award DTO
/// </summary>
public class TenderAwardDto
{
    public Guid Id { get; set; }
    public Guid TenderId { get; set; }
    public string TenderNumber { get; set; } = string.Empty;
    public string TenderTitle { get; set; } = string.Empty;

    /// <summary>
    /// LOT that was awarded (null for tender-level awards)
    /// </summary>
    public Guid? LotId { get; set; }
    public string? LotCode { get; set; }
    public string? LotTitle { get; set; }

    /// <summary>
    /// The LOT bid that won (null for tender-level awards)
    /// </summary>
    public Guid? BidLotId { get; set; }

    public Guid TenderBidId { get; set; }
    public string BidNumber { get; set; } = string.Empty;
    public Guid BusinessPartnerId { get; set; }
    public string BusinessPartnerName { get; set; } = string.Empty;
    public DateTime AwardDate { get; set; }

    /// <summary>
    /// Original bid amount before any negotiation
    /// </summary>
    public decimal OriginalBidAmount { get; set; }

    /// <summary>
    /// Final award amount (negotiated amount if negotiation exists, otherwise original bid amount)
    /// </summary>
    public decimal AwardedAmount { get; set; }

    /// <summary>
    /// Reference to the negotiation (if this award used negotiated prices)
    /// </summary>
    public Guid? NegotiationId { get; set; }

    /// <summary>
    /// Indicates if this award was based on a negotiated price
    /// </summary>
    public bool IsNegotiated { get; set; }

    /// <summary>
    /// Savings achieved through negotiation (OriginalBidAmount - AwardedAmount)
    /// </summary>
    public decimal NegotiationSavings { get; set; }

    public string? Currency { get; set; }
    public string Status { get; set; } = "Awarded";
    public string? AwardJustification { get; set; }
    public Guid? AwardedById { get; set; }
    public string? AwardedByName { get; set; }
    public Guid? PurchaseOrderId { get; set; }
    public string? Notes { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Create award DTO
/// </summary>
public class CreateAwardDto
{
    [Required]
    public Guid TenderId { get; set; }

    /// <summary>
    /// Optional LOT ID for per-LOT awards
    /// </summary>
    public Guid? LotId { get; set; }

    /// <summary>
    /// Optional LOT bid ID (if awarding a specific LOT bid)
    /// </summary>
    public Guid? BidLotId { get; set; }

    [Required]
    public Guid TenderBidId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal AwardedAmount { get; set; }

    [MaxLength(3)]
    public string? Currency { get; set; } = "USD";

    public DateTime? AwardDate { get; set; }
    public string? AwardJustification { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Award recommendation DTO
/// </summary>
public class AwardRecommendationDto
{
    public Guid TenderId { get; set; }
    public string TenderNumber { get; set; } = string.Empty;
    public string TenderTitle { get; set; } = string.Empty;

    /// <summary>
    /// LOT ID if this is a per-LOT recommendation
    /// </summary>
    public Guid? LotId { get; set; }
    public string? LotCode { get; set; }
    public string? LotTitle { get; set; }

    public int TotalBids { get; set; }
    public int EvaluatedBids { get; set; }
    public Guid? RecommendedBidId { get; set; }
    public Guid? RecommendedBidLotId { get; set; }
    public string? RecommendedBidNumber { get; set; }
    public string? RecommendedBusinessPartner { get; set; }
    public decimal RecommendedAmount { get; set; }
    public decimal RecommendedScore { get; set; }
    public List<BidRecommendationDto> BidRecommendations { get; set; } = new();
    public List<LotRecommendationDto> LotRecommendations { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
    public Guid? GeneratedById { get; set; }
}

/// <summary>
/// LOT-level recommendation DTO
/// </summary>
public class LotRecommendationDto
{
    public Guid LotId { get; set; }
    public string LotCode { get; set; } = string.Empty;
    public string LotTitle { get; set; } = string.Empty;
    public int TotalBidLots { get; set; }
    public int EvaluatedBidLots { get; set; }
    public Guid? RecommendedBidLotId { get; set; }
    public Guid? RecommendedBidId { get; set; }
    public string? RecommendedBidNumber { get; set; }
    public string? RecommendedBusinessPartner { get; set; }
    public decimal RecommendedAmount { get; set; }
    public decimal RecommendedScore { get; set; }
    public List<BidLotRecommendationDto> BidLotRecommendations { get; set; } = new();
}

/// <summary>
/// Bid LOT recommendation DTO
/// </summary>
public class BidLotRecommendationDto
{
    public Guid BidLotId { get; set; }
    public Guid BidId { get; set; }
    public string BidNumber { get; set; } = string.Empty;
    public Guid BusinessPartnerId { get; set; }
    public string BusinessPartnerName { get; set; } = string.Empty;
    public decimal TotalLotAmount { get; set; }
    public decimal AverageScore { get; set; }
    public int EvaluationCount { get; set; }
    public int RecommendationCount { get; set; }
    public int TotalEvaluators { get; set; }
    public string? Recommendation { get; set; }
}

/// <summary>
/// Bid recommendation DTO
/// </summary>
public class BidRecommendationDto
{
    public Guid BidId { get; set; }
    public string BidNumber { get; set; } = string.Empty;
    public Guid BusinessPartnerId { get; set; }
    public string BusinessPartnerName { get; set; } = string.Empty;
    public decimal TotalBidAmount { get; set; }
    public decimal AverageScore { get; set; }
    public int EvaluationCount { get; set; }

    /// <summary>
    /// Number of evaluators who recommended this bid
    /// </summary>
    public int RecommendationCount { get; set; }

    /// <summary>
    /// Total number of evaluators who submitted evaluations for this bid
    /// </summary>
    public int TotalEvaluators { get; set; }

    public string? Recommendation { get; set; }
}

/// <summary>
/// Approve award DTO
/// </summary>
public class ApproveAwardDto
{
    public string? Notes { get; set; }
}

/// <summary>
/// Reject award DTO
/// </summary>
public class RejectAwardDto
{
    [Required]
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Award notification DTO
/// </summary>
public class AwardNotificationDto
{
    public Guid AwardId { get; set; }
    public Guid TenderId { get; set; }
    public string TenderNumber { get; set; } = string.Empty;
    public string TenderTitle { get; set; } = string.Empty;
    public Guid BusinessPartnerId { get; set; }
    public string BusinessPartnerName { get; set; } = string.Empty;
    public decimal AwardAmount { get; set; }
    public DateTime AwardDate { get; set; }
    public DateTime? ContractStartDate { get; set; }
    public DateTime? ContractEndDate { get; set; }
    public DateTime NotificationDate { get; set; }
}

/// <summary>
/// Cancel award DTO
/// </summary>
public class CancelAwardDto
{
    [Required]
    public string Reason { get; set; } = string.Empty;
    
    public bool SendNotifications { get; set; } = true;
}

/// <summary>
/// Tender template DTO
/// </summary>
public class TenderTemplateDto
{
    public Guid Id { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string TenderType { get; set; } = "RFQ";
    public string Category { get; set; } = "General";
    public decimal PriceWeightage { get; set; } = 60;
    public decimal QualityWeightage { get; set; } = 20;
    public decimal DeliveryWeightage { get; set; } = 10;
    public decimal ExperienceWeightage { get; set; } = 10;
    public string? EvaluationCriteriaJson { get; set; }
    public int? DefaultValidityDays { get; set; }
    public string? RequiredDocuments { get; set; }
    public string? TermsAndConditions { get; set; }
    public bool RequiresPrequalification { get; set; }
    public bool AllowPartialBids { get; set; }
    public bool IsActive { get; set; }
    public Guid? CreatedById { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Create tender template DTO
/// </summary>
public class CreateTenderTemplateDto
{
    [Required]
    [MaxLength(100)]
    public string TemplateName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(50)]
    public string TenderType { get; set; } = "RFQ";

    [MaxLength(50)]
    public string Category { get; set; } = "General";

    [Range(0, 100)]
    public decimal PriceWeightage { get; set; } = 60;

    [Range(0, 100)]
    public decimal QualityWeightage { get; set; } = 20;

    [Range(0, 100)]
    public decimal DeliveryWeightage { get; set; } = 10;

    [Range(0, 100)]
    public decimal ExperienceWeightage { get; set; } = 10;

    public string? EvaluationCriteriaJson { get; set; }
    public int? DefaultValidityDays { get; set; }
    public string? RequiredDocuments { get; set; }
    public string? TermsAndConditions { get; set; }
    public bool RequiresPrequalification { get; set; } = false;
    public bool AllowPartialBids { get; set; } = false;
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Update tender template DTO
/// </summary>
public class UpdateTenderTemplateDto
{
    [Required]
    [MaxLength(100)]
    public string TemplateName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(50)]
    public string TenderType { get; set; } = "RFQ";

    [MaxLength(50)]
    public string Category { get; set; } = "General";

    [Range(0, 100)]
    public decimal PriceWeightage { get; set; } = 60;

    [Range(0, 100)]
    public decimal QualityWeightage { get; set; } = 20;

    [Range(0, 100)]
    public decimal DeliveryWeightage { get; set; } = 10;

    [Range(0, 100)]
    public decimal ExperienceWeightage { get; set; } = 10;

    public string? EvaluationCriteriaJson { get; set; }
    public int? DefaultValidityDays { get; set; }
    public string? RequiredDocuments { get; set; }
    public string? TermsAndConditions { get; set; }
    public bool RequiresPrequalification { get; set; }
    public bool AllowPartialBids { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Create tender from template DTO
/// </summary>
public class CreateTenderFromTemplateDto
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required]
    public DateTime PublicationDate { get; set; }

    public DateTime? ClosingDate { get; set; }
}

/// <summary>
/// Tender negotiation DTO
/// </summary>
public class TenderNegotiationDto
{
    public Guid Id { get; set; }
    public Guid TenderId { get; set; }
    public string? TenderNumber { get; set; }
    public string? TenderTitle { get; set; }
    public Guid TenderBidId { get; set; }
    public string? BidNumber { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public string? BusinessPartnerName { get; set; }
    public Guid? LotId { get; set; }
    public string? LotCode { get; set; }
    public string? LotTitle { get; set; }
    public Guid? BidLotId { get; set; }
    public string Status { get; set; } = "Invited";
    public DateTime InvitedDate { get; set; }
    public Guid? InvitedById { get; set; }
    public string? InvitedByName { get; set; }
    public DateTime? CompletedDate { get; set; }
    public Guid? CompletedById { get; set; }
    public string? CompletedByName { get; set; }
    public decimal OriginalAmount { get; set; }
    public decimal? NegotiatedAmount { get; set; }
    public string? Currency { get; set; }
    public string? Notes { get; set; }
    public List<TenderNegotiationItemDto> Items { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Tender negotiation item DTO
/// </summary>
public class TenderNegotiationItemDto
{
    public Guid Id { get; set; }
    public Guid NegotiationId { get; set; }
    public Guid TenderBidItemId { get; set; }
    public string ItemDescription { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string? UnitOfMeasure { get; set; }
    public decimal OriginalUnitPrice { get; set; }
    public decimal OriginalTotalPrice { get; set; }
    public decimal? NegotiatedUnitPrice { get; set; }
    public decimal? NegotiatedTotalPrice { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Create negotiation DTO - used to invite a bidder for negotiation
/// </summary>
public class CreateNegotiationDto
{
    [Required]
    public Guid TenderId { get; set; }

    [Required]
    public Guid TenderBidId { get; set; }

    /// <summary>
    /// Optional LOT ID for per-LOT negotiations
    /// </summary>
    public Guid? LotId { get; set; }

    /// <summary>
    /// Optional bid LOT ID for per-LOT negotiations
    /// </summary>
    public Guid? BidLotId { get; set; }

    public string? Notes { get; set; }
}

/// <summary>
/// Update negotiation item DTO - used to update negotiated prices
/// </summary>
public class UpdateNegotiationItemDto
{
    [Required]
    public Guid ItemId { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? NegotiatedUnitPrice { get; set; }

    public string? Notes { get; set; }
}

/// <summary>
/// Complete negotiation DTO
/// </summary>
public class CompleteNegotiationDto
{
    public List<UpdateNegotiationItemDto> Items { get; set; } = new();
    public string? Notes { get; set; }
}
