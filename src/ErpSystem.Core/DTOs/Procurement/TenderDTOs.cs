using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Procurement;

/// <summary>
/// Tender list DTO for grid display
/// </summary>
public class TenderDto
{
    public Guid Id { get; set; }
    public string TenderNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string TenderType { get; set; } = "RFQ";
    public string Status { get; set; } = "Draft";
    public DateTime? PublishDate { get; set; }
    public DateTime? SubmissionDeadline { get; set; }
    public decimal? EstimatedValue { get; set; }
    public string? Currency { get; set; }
    public int BidCount { get; set; }
    public int InvitationCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedByName { get; set; }
    public Guid? SourcePurchaseRequisitionId { get; set; }
    public Guid? SourcingReleaseId { get; set; }
    public Guid? SourcingCaseId { get; set; }
    public ProcurementMethodType? SourcingMethod { get; set; }

    // Workflow display helpers (optional)
    public string? CurrentWorkflowStepName { get; set; }
}

/// <summary>
/// Tender summary DTO for dashboard
/// </summary>
public class TenderSummaryDto
{
    public Guid Id { get; set; }
    public string TenderNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public DateTime? SubmissionDeadline { get; set; }
    public int BidCount { get; set; }
    public int DaysRemaining { get; set; }
}

/// <summary>
/// Tender detail DTO with all related data
/// </summary>
public class TenderDetailDto
{
    public bool UsesControlledTenderLifecycle { get; set; }
    public int BidCount { get; set; }
    public Guid Id { get; set; }
    public string TenderNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string TenderType { get; set; } = "RFQ";
    public string Status { get; set; } = "Draft";
    public DateTime? PublishDate { get; set; }
    public DateTime? SubmissionDeadline { get; set; }
    public DateTime? OpeningDate { get; set; }
    public DateTime? AwardDate { get; set; }
    public decimal? EstimatedValue { get; set; }
    public string? Currency { get; set; }
    public Guid? SourcePurchaseRequisitionId { get; set; }
    public Guid? SourcingReleaseId { get; set; }
    public Guid? SourcingCaseId { get; set; }
    public ProcurementMethodType? SourcingMethod { get; set; }

    // Eligibility
    public decimal? MinimumPerformanceRating { get; set; }
    public bool RequiresPrequalification { get; set; }
    public bool AllowPartialBids { get; set; }

    // Evaluation Criteria
    public decimal PriceWeightage { get; set; }
    public decimal QualityWeightage { get; set; }
    public decimal DeliveryWeightage { get; set; }
    public decimal ExperienceWeightage { get; set; }
    public string? EvaluationCriteriaJson { get; set; }

    // QCBS Configuration
    public bool UseQCBSEvaluation { get; set; }
    public decimal MinimumTechnicalScore { get; set; } = 80;
    public decimal TechnicalWeight { get; set; } = 60;
    public decimal FinancialWeight { get; set; } = 40;

    // Metadata
    public string? Notes { get; set; }
    public string? TermsAndConditions { get; set; }
    public int? BidValidityPeriodDays { get; set; }
    public string? RequiredDocuments { get; set; } // JSON array of required document types

    // Acceptance Declaration
    public bool RequiresAcceptanceDeclaration { get; set; }
    public string? AcceptanceDeclarationDocumentPath { get; set; }
    public string? AcceptanceDeclarationDocumentName { get; set; }

    // Evaluation Template
    public Guid? EvaluationTemplateId { get; set; }
    public string? EvaluationTemplateName { get; set; }

    public Guid? CreatedById { get; set; }
    public string? CreatedByName { get; set; }
    public Guid? PublishedById { get; set; }
    public string? PublishedByName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Related Data
    public List<TenderLotDto> Lots { get; set; } = new();
    public List<TenderItemDto> Items { get; set; } = new();
    public List<TenderDocumentDto> Documents { get; set; } = new();
    public List<TenderInvitationDto> Invitations { get; set; } = new();
    public List<TenderBidSummaryDto> Bids { get; set; } = new();
    public List<TenderFeeDto> Fees { get; set; } = new();
    public List<TenderEvaluatorDto> Evaluators { get; set; } = new();
    public List<TenderClarificationDto> Clarifications { get; set; } = new();
    public List<TenderRevisionDto> Revisions { get; set; } = new();

    // Statistics
    public int TotalViews { get; set; }
    public int TotalDownloads { get; set; }
    public int LotCount { get; set; }

    // Workflow display helpers (optional)
    public string? CurrentWorkflowStepName { get; set; }
}

/// <summary>
/// Create tender DTO
/// </summary>
public class CreateTenderDto
{
    [Required]
    public Guid SourcePurchaseRequisitionId { get; set; }

    public Guid? SourceProcurementPlanItemId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required]
    [MaxLength(50)]
    public string TenderType { get; set; } = "RFQ";

    public DateTime? SubmissionDeadline { get; set; }

    public DateTime? OpeningDate { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? EstimatedValue { get; set; }

    [MaxLength(3)]
    public string? Currency { get; set; } = "USD";

    // Eligibility
    [Range(0, 5)]
    public decimal? MinimumPerformanceRating { get; set; }

    public bool RequiresPrequalification { get; set; } = false;
    public bool AllowPartialBids { get; set; } = false;

    // Evaluation Criteria
    [Range(0, 100)]
    public decimal PriceWeightage { get; set; } = 60;

    [Range(0, 100)]
    public decimal QualityWeightage { get; set; } = 20;

    [Range(0, 100)]
    public decimal DeliveryWeightage { get; set; } = 10;

    [Range(0, 100)]
    public decimal ExperienceWeightage { get; set; } = 10;

    public string? EvaluationCriteriaJson { get; set; }

    // QCBS Configuration
    public bool UseQCBSEvaluation { get; set; } = false;

    [Range(0, 100)]
    public decimal MinimumTechnicalScore { get; set; } = 80;

    [Range(0, 100)]
    public decimal TechnicalWeight { get; set; } = 60;

    [Range(0, 100)]
    public decimal FinancialWeight { get; set; } = 40;

    public string? Notes { get; set; }
    public string? TermsAndConditions { get; set; }
    [Range(1, int.MaxValue)]
    public int? BidValidityPeriodDays { get; set; }
    public string? RequiredDocuments { get; set; } // JSON array of required document types

    // Acceptance Declaration
    public bool RequiresAcceptanceDeclaration { get; set; } = false;

    // Evaluation Template
    public Guid? EvaluationTemplateId { get; set; }

    public List<CreateTenderItemDto> Items { get; set; } = new();
}




/// <summary>
/// Update tender DTO
/// </summary>
public class UpdateTenderDto
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime? SubmissionDeadline { get; set; }
    public DateTime? OpeningDate { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? EstimatedValue { get; set; }

    [MaxLength(3)]
    public string? Currency { get; set; }

    // Eligibility
    [Range(0, 5)]
    public decimal? MinimumPerformanceRating { get; set; }

    public bool RequiresPrequalification { get; set; }
    public bool AllowPartialBids { get; set; }

    // Evaluation Criteria
    [Range(0, 100)]
    public decimal PriceWeightage { get; set; }

    [Range(0, 100)]
    public decimal QualityWeightage { get; set; }

    [Range(0, 100)]
    public decimal DeliveryWeightage { get; set; }

    [Range(0, 100)]
    public decimal ExperienceWeightage { get; set; }

    public string? EvaluationCriteriaJson { get; set; }

    // QCBS Configuration
    public bool UseQCBSEvaluation { get; set; } = false;

    [Range(0, 100)]
    public decimal MinimumTechnicalScore { get; set; } = 80;

    [Range(0, 100)]
    public decimal TechnicalWeight { get; set; } = 60;

    [Range(0, 100)]
    public decimal FinancialWeight { get; set; } = 40;

    public string? Notes { get; set; }
    public string? TermsAndConditions { get; set; }
    [Range(1, int.MaxValue)]
    public int? BidValidityPeriodDays { get; set; }
    public string? RequiredDocuments { get; set; } // JSON array of required document types

    // Acceptance Declaration
    public bool RequiresAcceptanceDeclaration { get; set; } = false;

    // Evaluation Template
    public Guid? EvaluationTemplateId { get; set; }
}

/// <summary>
/// Publish tender DTO
/// </summary>
public class PublishTenderDto
{
    [Required]
    public DateTime SubmissionDeadline { get; set; }

    public DateTime? OpeningDate { get; set; }

    public List<Guid> InvitedBusinessPartnerIds { get; set; } = new();

    /// <summary>
    /// Optional external/public recipients to receive the RFQ/Tender email (email-only; no in-app).
    /// Each email will be sent individually (no shared BCC list) for privacy.
    /// </summary>
    public List<string> ExternalRecipientEmails { get; set; } = new();

    public bool SendNotifications { get; set; } = true;

    // Required statutory advertisement controls for NCT/ICT sourcing cases.
    [MaxLength(200)]
    public string? AdvertisementReference { get; set; }
    [MaxLength(200)]
    public string? PublicationChannel { get; set; }
    [MaxLength(200)]
    public string? TenderDocumentReference { get; set; }
    [MaxLength(100)]
    public string? TenderDocumentVersion { get; set; }
    [Range(0, double.MaxValue)]
    public decimal DocumentFee { get; set; }
    [MaxLength(500)]
    public string? AdvertisementEvidenceReference { get; set; }
}

/// <summary>
/// Tender item DTO
/// </summary>
public class TenderItemDto
{
    public Guid Id { get; set; }
    public Guid TenderId { get; set; }
    public Guid? LotId { get; set; }
    public string? LotCode { get; set; }
    public string? LotTitle { get; set; }
    public int LineNumber { get; set; }
    public string? ItemCode { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string? UnitOfMeasure { get; set; }
    public string? Specifications { get; set; }
    public DateTime? RequiredDeliveryDate { get; set; }
    public string? DeliveryLocation { get; set; }
}

/// <summary>
/// Create tender item DTO
/// </summary>
public class CreateTenderItemDto
{
    /// <summary>
    /// Optional LOT ID to assign this item to
    /// </summary>
    public Guid? LotId { get; set; }

    public int LineNumber { get; set; }

    [MaxLength(200)]
    public string? ItemCode { get; set; }

    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Range(0.0001, double.MaxValue)]
    public decimal Quantity { get; set; }

    [MaxLength(20)]
    public string? UnitOfMeasure { get; set; }

    public string? Specifications { get; set; }
    public DateTime? RequiredDeliveryDate { get; set; }

    [MaxLength(200)]
    public string? DeliveryLocation { get; set; }
}

/// <summary>
/// Tender document DTO
/// </summary>
public class TenderDocumentDto
{
    public Guid Id { get; set; }
    public Guid TenderId { get; set; }
    public string DocumentName { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? FileType { get; set; }
    public long? FileSize { get; set; }
    public DateTime UploadedDate { get; set; }
    public string? UploadedByName { get; set; }
    public bool IsPublic { get; set; }
    public Guid? FileUploadRecordId { get; set; }
    public Guid? CentralDocumentRecordId { get; set; }
    public Guid? CentralDocumentVersionId { get; set; }
}

/// <summary>
/// Upload tender document DTO
/// </summary>
public class UploadTenderDocumentDto
{
    [Required]
    [MaxLength(200)]
    public string DocumentName { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string DocumentType { get; set; } = string.Empty;

    public bool IsPublic { get; set; } = true;
}

/// <summary>
/// Tender invitation DTO
/// </summary>
public class TenderInvitationDto
{
    public Guid Id { get; set; }
    public Guid TenderId { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public string BusinessPartnerName { get; set; } = string.Empty;
    public DateTime InvitedDate { get; set; }
    public string? InvitedByName { get; set; }
    public string Status { get; set; } = "Invited";
    public DateTime? ViewedDate { get; set; }
    public DateTime? ResponseDate { get; set; }
    public string? DeclineReason { get; set; }
}


/// <summary>
/// Invite tenderers DTO
/// </summary>
public class InviteTenderersDto
{
    [Required]
    public List<Guid> BusinessPartnerIds { get; set; } = new();

    /// <summary>
    /// Optional external/public recipients to receive the RFQ/Tender email (email-only; no in-app).
    /// Each email will be sent individually (no shared BCC list) for privacy.
    /// </summary>
    public List<string> ExternalRecipientEmails { get; set; } = new();

    public bool SendNotifications { get; set; } = true;
}

/// <summary>
/// Tender fee DTO
/// </summary>
public class TenderFeeDto
{
    public Guid Id { get; set; }
    public Guid TenderId { get; set; }
    public string FeeType { get; set; } = "DocumentFee";
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string PaymentMethod { get; set; } = "Online";
    public Guid? ReceivingAccountId { get; set; }
    public Guid? RevenueAccountId { get; set; }
    public bool IsMandatory { get; set; }
    public DateTime? DueDate { get; set; }
    public string? Description { get; set; }
    public string? BankAccountDetails { get; set; }
    public int PaymentCount { get; set; }
}

/// <summary>
/// Create tender fee DTO
/// </summary>
public class CreateTenderFeeDto
{
    [Required]
    [MaxLength(100)]
    public string FeeType { get; set; } = "DocumentFee";

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = "USD";

    [Required]
    [MaxLength(50)]
    public string PaymentMethod { get; set; } = "Online";

    public Guid? ReceivingAccountId { get; set; }

    public Guid? RevenueAccountId { get; set; }

    public bool IsMandatory { get; set; } = true;
    public DateTime? DueDate { get; set; }
    public string? Description { get; set; }

    [MaxLength(200)]
    public string? BankAccountDetails { get; set; }
}

/// <summary>
/// Tender evaluator DTO
/// </summary>
public class TenderEvaluatorDto
{
    public Guid Id { get; set; }
    public Guid TenderId { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Role { get; set; } = "Evaluator";
    public DateTime AssignedDate { get; set; }
    public string? AssignedByName { get; set; }
    public string Status { get; set; } = "Assigned";
    public DateTime? AcceptedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public decimal? WeightagePercentage { get; set; }
    public int EvaluationCount { get; set; }
}

/// <summary>
/// Active, tenant-scoped user who is authorised to evaluate tenders.
/// </summary>
public class TenderEvaluatorCandidateDto
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public List<string> RoleNames { get; set; } = new();
}

/// <summary>
/// Assign evaluators DTO
/// </summary>
public class AssignEvaluatorsDto
{
    [Required]
    public List<EvaluatorAssignmentDto> Evaluators { get; set; } = new();
}

/// <summary>
/// Evaluator assignment DTO
/// </summary>
public class EvaluatorAssignmentDto
{
    [Required]
    public Guid UserId { get; set; }

    [Required]
    [MaxLength(50)]
    public string Role { get; set; } = "Evaluator";

    [Range(0, 100)]
    public decimal? WeightagePercentage { get; set; }
}

/// <summary>
/// Tender clarification DTO
/// </summary>
public class TenderClarificationDto
{
    public Guid Id { get; set; }
    public Guid TenderId { get; set; }
    public Guid? BusinessPartnerId { get; set; }
    public string? BusinessPartnerName { get; set; }
    public string Question { get; set; } = string.Empty;
    public DateTime QuestionDate { get; set; }
    public string? QuestionByName { get; set; }
    public string? Answer { get; set; }
    public DateTime? AnswerDate { get; set; }
    public string? AnsweredByName { get; set; }
    public string Status { get; set; } = "Pending";
    public bool IsPublic { get; set; }
    public string? Category { get; set; }
}

/// <summary>
/// Create clarification DTO
/// </summary>
public class CreateClarificationDto
{
    [Required]
    public string Question { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Category { get; set; }

    public bool IsPublic { get; set; } = true;
}

/// <summary>
/// Answer clarification DTO
/// </summary>
public class AnswerClarificationDto
{
    [Required]
    public string Answer { get; set; } = string.Empty;

    public bool IsPublic { get; set; } = true;
}

/// <summary>
/// Tender revision DTO
/// </summary>
public class TenderRevisionDto
{
    public Guid Id { get; set; }
    public Guid TenderId { get; set; }
    public string RevisionNumber { get; set; } = string.Empty;
    public DateTime RevisionDate { get; set; }
    public string? RevisedByName { get; set; }
    public string RevisionType { get; set; } = "Amendment";
    public string Description { get; set; } = string.Empty;
    public string? Changes { get; set; }
    public DateTime? NewSubmissionDeadline { get; set; }
    public bool RequiresRebid { get; set; }
    public bool NotificationSent { get; set; }
}

/// <summary>
/// Create revision DTO
/// </summary>
public class CreateRevisionDto
{
    [Required]
    [MaxLength(200)]
    public string RevisionType { get; set; } = "Amendment";

    [Required]
    public string Description { get; set; } = string.Empty;

    public string? Changes { get; set; }
    public DateTime? NewSubmissionDeadline { get; set; }
    public bool RequiresRebid { get; set; } = false;
    public bool SendNotifications { get; set; } = true;
}

/// <summary>
/// Tender document requirement DTO
/// </summary>
public class TenderDocumentRequirementDto
{
    [Required]
    [MaxLength(100)]
    public string DocumentType { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string DocumentName { get; set; } = string.Empty;

    public bool IsRequired { get; set; } = true;

    [MaxLength(500)]
    public string? Description { get; set; }

    public int? MaxFileSizeMB { get; set; }

    public string? AllowedFileTypes { get; set; } // e.g., "PDF,DOC,DOCX"
}

#region Tender LOT DTOs

/// <summary>
/// Tender LOT DTO - for display
/// </summary>
public class TenderLotDto
{
    public Guid Id { get; set; }
    public Guid TenderId { get; set; }
    public int LotNumber { get; set; }
    public string LotCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal? EstimatedValue { get; set; }
    public string? Currency { get; set; }
    public string Status { get; set; } = "Active";
    public DateTime? RequiredDeliveryDate { get; set; }
    public string? DeliveryLocation { get; set; }
    public string? Specifications { get; set; }
    public string? Notes { get; set; }
    public int DisplayOrder { get; set; }
    public int ItemCount { get; set; }
    public int BidCount { get; set; }
    public bool IsAwarded { get; set; }
    public string? AwardedToPartnerName { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<TenderItemDto> Items { get; set; } = new();
}

/// <summary>
/// Create tender LOT DTO
/// </summary>
public class CreateTenderLotDto
{
    public int LotNumber { get; set; }

    [Required]
    [MaxLength(100)]
    public string LotCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? EstimatedValue { get; set; }

    [MaxLength(3)]
    public string? Currency { get; set; } = "USD";

    public DateTime? RequiredDeliveryDate { get; set; }

    [MaxLength(200)]
    public string? DeliveryLocation { get; set; }

    public string? Specifications { get; set; }

    public string? Notes { get; set; }

    public int DisplayOrder { get; set; } = 0;

    /// <summary>
    /// Items to add to this LOT
    /// </summary>
    public List<CreateTenderItemDto> Items { get; set; } = new();
}

/// <summary>
/// Update tender LOT DTO
/// </summary>
public class UpdateTenderLotDto
{
    [Required]
    [MaxLength(100)]
    public string LotCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? EstimatedValue { get; set; }

    [MaxLength(3)]
    public string? Currency { get; set; }

    public DateTime? RequiredDeliveryDate { get; set; }

    [MaxLength(200)]
    public string? DeliveryLocation { get; set; }

    public string? Specifications { get; set; }

    public string? Notes { get; set; }

    public int DisplayOrder { get; set; }
}

/// <summary>
/// DTO for assigning items to a LOT
/// </summary>
public class AssignItemsToLotDto
{
    [Required]
    public List<Guid> ItemIds { get; set; } = new();
}

#endregion
