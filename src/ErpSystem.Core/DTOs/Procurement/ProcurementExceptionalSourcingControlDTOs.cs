using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Procurement;

public sealed class ProcurementExceptionalSourcingReadinessDto
{
    public Guid? SourceRequisitionId { get; set; }
    public string Currency { get; set; } = string.Empty;
    public decimal? EstimatedValue { get; set; }
    public List<PettyPurchaseQuotationItemDto> QuotationItems { get; set; } = new();
    public Guid TenderId { get; set; }
    public string TenderNumber { get; set; } = string.Empty;
    public string TenderTitle { get; set; } = string.Empty;
    public string TenderStatus { get; set; } = string.Empty;
    public ProcurementMethodType Method { get; set; }
    public string MethodRuleCode { get; set; } = string.Empty;
    public string ExceptionRuleCode { get; set; } = string.Empty;
    public string AuthorityRouteReference { get; set; } = string.Empty;
    public int MinimumSupplierCount { get; set; }
    public bool BoardApprovalRequired { get; set; }
    public bool ManagingDirectorApprovalRequired { get; set; }
    public bool PpaApprovalRequired { get; set; }
    public bool JustificationRequired { get; set; }
    public bool EvidenceRequired { get; set; }
    public bool PostAwardFilingRequired { get; set; }
    public List<ProcurementExceptionalEvidenceRequirementDto> EvidenceRequirements { get; set; } = new();
    public List<ProcurementExceptionalSupplierOptionDto> SupplierOptions { get; set; } = new();
}

public sealed class ProcurementExceptionalEvidenceRequirementDto
{
    public Guid EvidenceRuleId { get; set; }
    public string RuleCode { get; set; } = string.Empty;
    public string RequirementKey { get; set; } = string.Empty;
    public string EvidenceName { get; set; } = string.Empty;
    public bool RequiresVerification { get; set; }
}

public sealed class ProcurementExceptionalSupplierOptionDto
{
    public Guid BusinessPartnerId { get; set; }
    public string PartnerCode { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string RegistrationStatus { get; set; } = string.Empty;
}

public sealed class ProcurementExceptionalSourcingControlDto
{
    public Guid? SourceRequisitionId { get; set; }
    public Guid TenderId { get; set; }
    public string TenderNumber { get; set; } = string.Empty;
    public string TenderTitle { get; set; } = string.Empty;
    public ProcurementMethodType Method { get; set; }
    public string MethodRuleCode { get; set; } = string.Empty;
    public string ExceptionRuleCode { get; set; } = string.Empty;
    public string AuthorityRouteReference { get; set; } = string.Empty;
    public ProcurementExceptionalSourcingControlStatus Status { get; set; }
    public string Justification { get; set; } = string.Empty;
    public string JustificationEvidenceReference { get; set; } = string.Empty;
    public string SupplierSelectionEvidenceReference { get; set; } = string.Empty;
    public DateTime PreparedAtUtc { get; set; }
    public DateTime? SuppliersInvitedAtUtc { get; set; }
    public bool BoardApprovalRequired { get; set; }
    public bool ManagingDirectorApprovalRequired { get; set; }
    public bool PpaApprovalRequired { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public string? BoardApprovalReference { get; set; }
    public string? ManagingDirectorApprovalReference { get; set; }
    public string? PpaApprovalReference { get; set; }
    public Guid? NegotiationId { get; set; }
    public string? NegotiationPlanReference { get; set; }
    public string? NegotiationMinutesEvidenceReference { get; set; }
    public string? NegotiationOutcomeReference { get; set; }
    public decimal? NegotiatedAmount { get; set; }
    public Guid? RecommendedBidId { get; set; }
    public string? RecommendationReason { get; set; }
    public string? AwardReference { get; set; }
    public string? ContractReference { get; set; }
    public string? BidderAcceptanceReference { get; set; }
    public string? PostAwardFilingReference { get; set; }
    public string? ExceptionReportReference { get; set; }
    public string IntegrityHash { get; set; } = string.Empty;
    public string RowVersion { get; set; } = string.Empty;
    public List<ProcurementExceptionalSupplierDto> Suppliers { get; set; } = new();
    public List<ProcurementExceptionalEvidenceDto> EvidenceChecklist { get; set; } = new();
    public List<ProcurementExceptionalBidDto> Bids { get; set; } = new();
    public List<ProcurementExceptionalSourcingMilestoneDto> Milestones { get; set; } = new();
}

public sealed class ProcurementExceptionalBidDto
{
    public Guid BidId { get; set; }
    public string BidNumber { get; set; } = string.Empty;
    public Guid BusinessPartnerId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public decimal BidAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public sealed class ProcurementExceptionalSupplierDto
{
    public Guid BusinessPartnerId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
}

public sealed class ProcurementExceptionalEvidenceDto
{
    public Guid EvidenceRuleId { get; set; }
    public string RuleCode { get; set; } = string.Empty;
    public string RequirementKey { get; set; } = string.Empty;
    public string EvidenceName { get; set; } = string.Empty;
    public string EvidenceReference { get; set; } = string.Empty;
    public string VerificationReference { get; set; } = string.Empty;
}

public sealed class ProcurementExceptionalSourcingMilestoneDto
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public DateTime? CompletedAtUtc { get; set; }
    public string? Reference { get; set; }
}

public sealed class PrepareProcurementExceptionalSourcingRequest
{
    public PettyPurchaseQuotationRequest? Quotation { get; set; }
    [StringLength(2000)] public string Justification { get; set; } = string.Empty;
    [StringLength(500)] public string JustificationEvidenceReference { get; set; } = string.Empty;
    [Required, StringLength(500)] public string SupplierSelectionEvidenceReference { get; set; } = string.Empty;
    [MinLength(1)] public List<Guid> BusinessPartnerIds { get; set; } = new();
    [MinLength(1)] public List<ProcurementExceptionalEvidenceRequest> EvidenceChecklist { get; set; } = new();
}

public sealed class PettyPurchaseQuotationRequest
{
    [Required, StringLength(200)] public string Reference { get; set; } = string.Empty;
    [Required, StringLength(500)] public string EvidenceReference { get; set; } = string.Empty;
    [MinLength(1)] public List<PettyPurchaseQuotationPriceRequest> Items { get; set; } = new();
}

public sealed class PettyPurchaseQuotationPriceRequest
{
    public Guid TenderItemId { get; set; }
    public decimal UnitPrice { get; set; }
}

public sealed class PettyPurchaseQuotationItemDto
{
    public Guid TenderItemId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string? UnitOfMeasure { get; set; }
}

public sealed class ProcurementExceptionalEvidenceRequest
{
    [Required, StringLength(200)] public string RequirementKey { get; set; } = string.Empty;
    [Required, StringLength(500)] public string EvidenceReference { get; set; } = string.Empty;
    [Required, StringLength(500)] public string VerificationReference { get; set; } = string.Empty;
}

public sealed class SubmitProcurementExceptionalApprovalRequest
{
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class DecideProcurementExceptionalApprovalRequest
{
    [Required] public string Action { get; set; } = string.Empty;
    [StringLength(300)] public string? BoardApprovalReference { get; set; }
    [StringLength(300)] public string? ManagingDirectorApprovalReference { get; set; }
    [StringLength(300)] public string? PpaApprovalReference { get; set; }
    [StringLength(1000)] public string? Comments { get; set; }
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class RecordProcurementExceptionalNegotiationRequest
{
    public Guid NegotiationId { get; set; }
    [Required, StringLength(300)] public string PlanReference { get; set; } = string.Empty;
    [Required, StringLength(500)] public string MinutesEvidenceReference { get; set; } = string.Empty;
    [Required, StringLength(300)] public string OutcomeReference { get; set; } = string.Empty;
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class RecordProcurementExceptionalRecommendationRequest
{
    public Guid BidId { get; set; }
    [Required, StringLength(2000)] public string Reason { get; set; } = string.Empty;
    [Required, StringLength(500)] public string EvidenceReference { get; set; } = string.Empty;
    [Required] public string RowVersion { get; set; } = string.Empty;
}

public sealed class RecordProcurementPostAwardFilingRequest
{
    [Required, StringLength(300)] public string FilingReference { get; set; } = string.Empty;
    [Required, StringLength(500)] public string FilingEvidenceReference { get; set; } = string.Empty;
    [Required, StringLength(300)] public string ExceptionReportReference { get; set; } = string.Empty;
    [Required, StringLength(500)] public string ExceptionReportEvidenceReference { get; set; } = string.Empty;
    [Required] public string RowVersion { get; set; } = string.Empty;
}
