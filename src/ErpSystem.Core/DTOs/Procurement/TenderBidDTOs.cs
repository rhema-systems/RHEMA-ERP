using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Procurement;

/// <summary>
/// Tender bid summary DTO for listing
/// </summary>
public class TenderBidSummaryDto
{
    public bool IsSealed { get; set; }
    public bool IsFinancialProposalSealed { get; set; }
    public Guid Id { get; set; }
    public Guid TenderId { get; set; }
    public string TenderNumber { get; set; } = string.Empty;
    public string TenderTitle { get; set; } = string.Empty;
    public Guid BusinessPartnerId { get; set; }
    public string BusinessPartnerName { get; set; } = string.Empty;
    public string BidNumber { get; set; } = string.Empty;
    public DateTime SubmittedDate { get; set; }
    public string Status { get; set; } = "Submitted";
    public decimal TotalBidAmount { get; set; }
    public string? Currency { get; set; }
    public decimal? TotalScore { get; set; }
    public int? Rank { get; set; }
    public bool IsCompliant { get; set; }
    public bool HasPaidFees { get; set; }
    public string? PaymentStatus { get; set; }

    // QCBS Scores
    public decimal? TechnicalScore { get; set; }
    public decimal? FinancialScore { get; set; }
    public decimal? CombinedScore { get; set; }
    public bool IsQualifiedTechnically { get; set; } = true;
    public string? DisqualificationReason { get; set; }
}

/// <summary>
/// Tender bid detail DTO
/// </summary>
public class TenderBidDetailDto
{
    public bool IsSealed { get; set; }
    public bool IsFinancialProposalSealed { get; set; }
    public Guid Id { get; set; }
    public Guid TenderId { get; set; }
    public string TenderNumber { get; set; } = string.Empty;
    public string TenderTitle { get; set; } = string.Empty;
    public Guid BusinessPartnerId { get; set; }
    public string BusinessPartnerName { get; set; } = string.Empty;
    public string BidNumber { get; set; } = string.Empty;
    public DateTime SubmittedDate { get; set; }
    public string Status { get; set; } = "Submitted";
    public decimal TotalBidAmount { get; set; }
    public string? Currency { get; set; }
    public int? DeliveryDays { get; set; }
    public string? PaymentTerms { get; set; }
    public string? WarrantyTerms { get; set; }
    public string? TechnicalProposal { get; set; }
    public string? CommercialProposal { get; set; }

    // Association and Acceptance
    public string? AssociationType { get; set; }
    public bool AcceptedDeclaration { get; set; }
    public DateTime? DeclarationAcceptedAt { get; set; }

    public bool IsCompliant { get; set; }
    public string? NonComplianceReasons { get; set; }

    // Evaluation Template
    public Guid? EvaluationTemplateId { get; set; }
    public string? EvaluationTemplateName { get; set; }

    // Evaluation Scores
    public decimal? PriceScore { get; set; }
    public decimal? QualityScore { get; set; }
    public decimal? DeliveryScore { get; set; }
    public decimal? ExperienceScore { get; set; }
    public decimal? TotalScore { get; set; }
    public int? Rank { get; set; }

    // QCBS Scores
    public decimal? TechnicalScore { get; set; }
    public decimal? FinancialScore { get; set; }
    public decimal? CombinedScore { get; set; }
    public bool IsQualifiedTechnically { get; set; } = true;
    public string? DisqualificationReason { get; set; }

    // Metadata
    public DateTime? OpenedDate { get; set; }
    public string? OpenedByName { get; set; }
    public string? EvaluatedByName { get; set; }
    public DateTime? EvaluatedDate { get; set; }
    public string? EvaluationNotes { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Related Data
    public List<Guid> SelectedLotIds { get; set; } = new();
    public List<TenderBidLotDto> BidLots { get; set; } = new();
    public List<TenderBidItemDto> Items { get; set; } = new();
    public List<TenderBidDocumentDto> Documents { get; set; } = new();
    public List<TenderEvaluationDto> Evaluations { get; set; } = new();
    public List<TenderInterviewDto> Interviews { get; set; } = new();
    public int BidLotCount { get; set; }
}

/// <summary>
/// DTO for initiating a bid (Step 1-3: Association, Declaration, Payment)
/// </summary>
public class InitiateBidDto
{
    [Required]
    public Guid TenderId { get; set; }

    [Required]
    [MaxLength(50)]
    public string AssociationType { get; set; } = "Self"; // AllUsers, Self, SelectedUsers

    /// <summary>
    /// Required when AssignmentType is "SelectedUsers"
    /// </summary>
    public List<Guid>? SelectedUserIds { get; set; }

    public bool AcceptedDeclaration { get; set; } = false;
}

/// <summary>
/// Create tender bid DTO
/// </summary>
public class CreateTenderBidDto
{
    [Required]
    public Guid TenderId { get; set; }

    [Range(1, int.MaxValue)]
    public int? DeliveryDays { get; set; }

    [MaxLength(500)]
    public string? PaymentTerms { get; set; }

    [MaxLength(500)]
    public string? WarrantyTerms { get; set; }

    public string? TechnicalProposal { get; set; }
    public string? CommercialProposal { get; set; }

    // Association and Acceptance (New fields for GHANEPS-style flow)
    [MaxLength(50)]
    public string? AssociationType { get; set; } // AllUsers, Self, SelectedUsers

    public bool AcceptedDeclaration { get; set; } = false;

    public List<Guid> SelectedLotIds { get; set; } = new();

    [Required]
    public List<CreateTenderBidItemDto> Items { get; set; } = new();
}

/// <summary>
/// Update tender bid DTO
/// </summary>
public class UpdateTenderBidDto
{
    [Range(1, int.MaxValue)]
    public int? DeliveryDays { get; set; }

    [MaxLength(500)]
    public string? PaymentTerms { get; set; }

    [MaxLength(500)]
    public string? WarrantyTerms { get; set; }

    public string? TechnicalProposal { get; set; }
    public string? CommercialProposal { get; set; }

    [MaxLength(50)]
    public string? AssociationType { get; set; }

    public bool? AcceptedDeclaration { get; set; }

    public List<Guid>? SelectedLotIds { get; set; }

    public List<UpdateTenderBidItemDto>? Items { get; set; }
}

/// <summary>
/// Submit tender bid DTO
/// </summary>
public class SubmitTenderBidDto
{
    [Required]
    public bool ConfirmSubmission { get; set; }
}

/// <summary>
/// Withdraw tender bid DTO
/// </summary>
public class WithdrawTenderBidDto
{
    [Required]
    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// Tender bid item DTO
/// </summary>
public class TenderBidItemDto
{
    public Guid Id { get; set; }
    public Guid TenderBidId { get; set; }
    public Guid? BidLotId { get; set; }
    public string? LotCode { get; set; }
    public Guid TenderItemId { get; set; }
    public string TenderItemDescription { get; set; } = string.Empty;
    public decimal RequestedQuantity { get; set; }
    public decimal OfferedQuantity { get; set; }
    public string? UnitOfMeasure { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
    public int? DeliveryDays { get; set; }
    public string? Specifications { get; set; }
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? TechnicalDetails { get; set; }
}

/// <summary>
/// Update tender bid item DTO
/// </summary>
public class UpdateTenderBidItemDto
{
    [Required]
    public Guid TenderItemId { get; set; }

    [Required]
    [Range(0, double.MaxValue)]
    public decimal OfferedQuantity { get; set; }

    [Required]
    [Range(0, double.MaxValue)]
    public decimal UnitPrice { get; set; }

    [Range(1, int.MaxValue)]
    public int? DeliveryDays { get; set; }

    [MaxLength(500)]
    public string? Specifications { get; set; }

    [MaxLength(200)]
    public string? Brand { get; set; }

    [MaxLength(200)]
    public string? Model { get; set; }

    public string? TechnicalDetails { get; set; }
}

/// <summary>
/// Create tender bid item DTO
/// </summary>
public class CreateTenderBidItemDto
{
    [Required]
    public Guid TenderItemId { get; set; }

    [Required]
    [Range(0, double.MaxValue)]
    public decimal OfferedQuantity { get; set; }

    [Required]
    [Range(0, double.MaxValue)]
    public decimal UnitPrice { get; set; }

    [Range(1, int.MaxValue)]
    public int? DeliveryDays { get; set; }

    [MaxLength(500)]
    public string? Specifications { get; set; }

    [MaxLength(200)]
    public string? Brand { get; set; }

    [MaxLength(200)]
    public string? Model { get; set; }

    public string? TechnicalDetails { get; set; }
}

/// <summary>
/// Tender bid document DTO
/// </summary>
public class TenderBidDocumentDto
{
    public Guid Id { get; set; }
    public Guid TenderBidId { get; set; }
    public string DocumentName { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? FileType { get; set; }
    public long? FileSize { get; set; }
    public DateTime UploadedDate { get; set; }
    public string? UploadedByName { get; set; }
    public Guid? FileUploadRecordId { get; set; }
    public Guid? CentralDocumentRecordId { get; set; }
    public Guid? CentralDocumentVersionId { get; set; }
}

/// <summary>
/// Upload bid document DTO
/// </summary>
public class UploadBidDocumentDto
{
    [Required]
    [MaxLength(200)]
    public string DocumentName { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string DocumentType { get; set; } = string.Empty;
}

/// <summary>
/// Tender payment DTO
/// </summary>
public class TenderPaymentDto
{
    public Guid Id { get; set; }
    public Guid TenderFeeId { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public string BusinessPartnerName { get; set; } = string.Empty;
    public string PaymentReference { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string PaymentMethod { get; set; } = "Online";
    public string Status { get; set; } = "Pending";
    public DateTime PaymentDate { get; set; }
    public DateTime? VerifiedDate { get; set; }
    public string? VerifiedByName { get; set; }
    public Guid? PostingEventId { get; set; }
    public Guid? JournalEntryId { get; set; }
    public DateTime? PostedAtUtc { get; set; }
    public string? TransactionId { get; set; }
    public string? PaymentProof { get; set; }
}

/// <summary>
/// Server-derived initiation state for the current supplier and tender.
/// </summary>
public class TenderBidInitiationStatusDto
{
    public Guid TenderId { get; set; }
    public Guid BusinessPartnerId { get; set; }
    public Guid? DraftBidId { get; set; }
    public bool HasAssignment { get; set; }
    public string? AssignmentType { get; set; }
    public bool RequiresAcceptanceDeclaration { get; set; }
    public bool DeclarationAccepted { get; set; }
    public bool DeclarationSatisfied { get; set; }
    public bool PaymentRequired { get; set; }
    public bool HasPayment { get; set; }
    public bool PaymentSatisfied { get; set; }
    public bool PaymentEvidenceAccepted { get; set; }
    public bool PaymentPendingVerification { get; set; }
    public bool CanProceed { get; set; }
    public List<TenderFeePaymentStatusDto> Fees { get; set; } = new();
}

public class TenderFeePaymentStatusDto
{
    public Guid TenderFeeId { get; set; }
    public string FeeType { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public bool IsMandatory { get; set; }
    public string Status { get; set; } = "NotPaid";
    public Guid? PaymentId { get; set; }
}

/// <summary>
/// Create payment DTO
/// </summary>
public class CreateTenderPaymentDto
{
    [Required]
    public Guid TenderFeeId { get; set; }

    public Guid? TenderBidId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    public DateTime? PaymentDate { get; set; }

    [MaxLength(500)]
    public string? PaymentReference { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = "USD";

    [Required]
    [MaxLength(50)]
    public string PaymentMethod { get; set; } = "Online";

    [MaxLength(500)]
    public string? TransactionId { get; set; }

    [MaxLength(1000)]
    public string? PaymentProof { get; set; }

    public string? Notes { get; set; }
}

/// <summary>
/// Record payment DTO
/// </summary>
public class RecordPaymentDto
{
    [Required]
    public Guid TenderFeeId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal Amount { get; set; }

    [MaxLength(3)]
    public string Currency { get; set; } = "USD";

    [Required]
    [MaxLength(50)]
    public string PaymentMethod { get; set; } = "Online";

    [MaxLength(500)]
    public string? TransactionId { get; set; }

    [MaxLength(1000)]
    public string? PaymentProof { get; set; }

    public string? Notes { get; set; }
}

/// <summary>
/// Verify payment DTO
/// </summary>
public class VerifyPaymentDto
{
    [Required]
    public bool IsApproved { get; set; }

    public string? Notes { get; set; }
}

/// <summary>
/// Tender interview DTO
/// </summary>
public class TenderInterviewDto
{
    public Guid Id { get; set; }
    public Guid TenderId { get; set; }
    public Guid TenderBidId { get; set; }
    public string BusinessPartnerName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime ScheduledDate { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string InterviewType { get; set; } = "Virtual";
    public string? Location { get; set; }
    public string Status { get; set; } = "Scheduled";
    public string? Agenda { get; set; }
    public string? InterviewNotes { get; set; }
    public decimal? InterviewScore { get; set; }
    public string? ConductedByName { get; set; }
    public DateTime? CompletedDate { get; set; }
}

/// <summary>
/// Schedule interview DTO
/// </summary>
public class ScheduleInterviewDto
{
    [Required]
    public Guid TenderBidId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required]
    public DateTime ScheduledDate { get; set; }

    public DateTime? InterviewDate { get; set; }

    [MaxLength(500)]
    public string? MeetingLink { get; set; }

    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }

    [Required]
    [MaxLength(50)]
    public string InterviewType { get; set; } = "Virtual";

    [MaxLength(500)]
    public string? Location { get; set; }

    public string? Agenda { get; set; }
    public string? PanelMembers { get; set; }
}

/// <summary>
/// Update interview DTO
/// </summary>
public class UpdateInterviewDto
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required]
    public DateTime ScheduledDate { get; set; }

    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }

    [Required]
    [MaxLength(50)]
    public string InterviewType { get; set; } = "Virtual";

    [MaxLength(500)]
    public string? Location { get; set; }

    public string? Agenda { get; set; }
    public string? PanelMembers { get; set; }
    public string? Notes { get; set; }
}

/// <summary>
/// Supplier bid list item for reporting
/// </summary>
public class SupplierBidListItemDto
{
    public bool IsSealed { get; set; }
    public string BidNumber { get; set; } = string.Empty;
    public string BusinessPartnerName { get; set; } = string.Empty;
    public string BusinessPartnerCode { get; set; } = string.Empty;
    public DateTime SubmittedDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? OpenedDate { get; set; }
    public decimal TotalBidAmount { get; set; }
    public string? Currency { get; set; }
}

#region Tender Bid LOT DTOs

/// <summary>
/// Tender bid LOT DTO - represents a bid on a specific LOT
/// </summary>
public class TenderBidLotDto
{
    public Guid Id { get; set; }
    public Guid TenderBidId { get; set; }
    public Guid LotId { get; set; }
    public string LotCode { get; set; } = string.Empty;
    public string LotTitle { get; set; } = string.Empty;
    public decimal TotalLotAmount { get; set; }
    public string? Currency { get; set; }
    public int? DeliveryDays { get; set; }
    public string? PaymentTerms { get; set; }
    public string? WarrantyTerms { get; set; }
    public string? TechnicalProposal { get; set; }
    public string? CommercialProposal { get; set; }
    public string Status { get; set; } = "Draft";
    public decimal? PriceScore { get; set; }
    public decimal? QualityScore { get; set; }
    public decimal? DeliveryScore { get; set; }
    public decimal? TotalScore { get; set; }
    public int? Rank { get; set; }
    public string? EvaluationNotes { get; set; }
    public string? Notes { get; set; }
    public int ItemCount { get; set; }
    public List<TenderBidItemDto> Items { get; set; } = new();
}

/// <summary>
/// Create tender bid LOT DTO
/// </summary>
public class CreateTenderBidLotDto
{
    [Required]
    public Guid LotId { get; set; }

    public int? DeliveryDays { get; set; }

    [MaxLength(500)]
    public string? PaymentTerms { get; set; }

    [MaxLength(500)]
    public string? WarrantyTerms { get; set; }

    public string? TechnicalProposal { get; set; }
    public string? CommercialProposal { get; set; }
    public string? Notes { get; set; }

    /// <summary>
    /// All items in this LOT must be bid on
    /// </summary>
    [Required]
    public List<CreateTenderBidItemDto> Items { get; set; } = new();
}

/// <summary>
/// Update tender bid LOT DTO
/// </summary>
public class UpdateTenderBidLotDto
{
    public int? DeliveryDays { get; set; }

    [MaxLength(500)]
    public string? PaymentTerms { get; set; }

    [MaxLength(500)]
    public string? WarrantyTerms { get; set; }

    public string? TechnicalProposal { get; set; }
    public string? CommercialProposal { get; set; }
    public string? Notes { get; set; }

    public List<UpdateTenderBidItemDto>? Items { get; set; }
}

/// <summary>
/// Submit bid for specific LOTs
/// </summary>
public class SubmitBidLotsDto
{
    [Required]
    public List<Guid> LotIds { get; set; } = new();

    [Required]
    public bool ConfirmSubmission { get; set; }
}

#endregion
