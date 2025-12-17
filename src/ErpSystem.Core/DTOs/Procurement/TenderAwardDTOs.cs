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
    public Guid TenderBidId { get; set; }
    public string BidNumber { get; set; } = string.Empty;
    public Guid BusinessPartnerId { get; set; }
    public string BusinessPartnerName { get; set; } = string.Empty;
    public DateTime AwardDate { get; set; }
    public decimal AwardedAmount { get; set; }
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
    public int TotalBids { get; set; }
    public int EvaluatedBids { get; set; }
    public Guid? RecommendedBidId { get; set; }
    public string? RecommendedBidNumber { get; set; }
    public string? RecommendedBusinessPartner { get; set; }
    public decimal RecommendedAmount { get; set; }
    public decimal RecommendedScore { get; set; }
    public List<BidRecommendationDto> BidRecommendations { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
    public Guid? GeneratedById { get; set; }
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

