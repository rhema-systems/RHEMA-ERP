using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Sales;

// ═══════════════════════════════════════════════════════════════════════════
//  LEAD DTOs
// ═══════════════════════════════════════════════════════════════════════════

#region Lead DTOs

public class LeadSummaryDto
{
    public Guid Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string LeadSource { get; set; } = "Unknown";
    public string LeadStatus { get; set; } = "New";
    public int QualificationScore { get; set; }
    public decimal EstimatedValue { get; set; }
    public DateTime? LastContactDate { get; set; }
    public DateTime? NextFollowUpDate { get; set; }
    public string? AssignedToName { get; set; }
    public bool IsConverted { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class LeadDetailDto : LeadSummaryDto
{
    public string? JobTitle { get; set; }
    public string? Mobile { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; }
    public Guid? AssignedToId { get; set; }
    public Guid? ConvertedCustomerId { get; set; }
    public string? ConvertedCustomerName { get; set; }
    public DateTime? ConvertedDate { get; set; }
    public string? Notes { get; set; }
    public List<ActivitySummaryDto> Activities { get; set; } = new();
    public List<OpportunitySummaryDto> Opportunities { get; set; } = new();
}

public class CreateLeadDto
{
    [Required] [StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required] [StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    [StringLength(100)]
    public string? CompanyName { get; set; }

    [StringLength(100)]
    public string? JobTitle { get; set; }

    [StringLength(100)] [EmailAddress]
    public string? Email { get; set; }

    [StringLength(20)]
    public string? Phone { get; set; }

    [StringLength(20)]
    public string? Mobile { get; set; }

    [StringLength(200)]
    public string? AddressLine1 { get; set; }

    [StringLength(200)]
    public string? AddressLine2 { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(100)]
    public string? State { get; set; }

    [StringLength(20)]
    public string? PostalCode { get; set; }

    [StringLength(100)]
    public string? Country { get; set; }

    public string LeadSource { get; set; } = "Unknown";
    public decimal? EstimatedValue { get; set; }
    public Guid? AssignedToId { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }
}

public class UpdateLeadDto
{
    [StringLength(100)]
    public string? FirstName { get; set; }

    [StringLength(100)]
    public string? LastName { get; set; }

    [StringLength(100)]
    public string? CompanyName { get; set; }

    [StringLength(100)]
    public string? JobTitle { get; set; }

    [StringLength(100)] [EmailAddress]
    public string? Email { get; set; }

    [StringLength(20)]
    public string? Phone { get; set; }

    [StringLength(20)]
    public string? Mobile { get; set; }

    [StringLength(200)]
    public string? AddressLine1 { get; set; }

    [StringLength(200)]
    public string? AddressLine2 { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(100)]
    public string? State { get; set; }

    [StringLength(20)]
    public string? PostalCode { get; set; }

    [StringLength(100)]
    public string? Country { get; set; }

    public string? LeadSource { get; set; }
    public string? LeadStatus { get; set; }
    public int? QualificationScore { get; set; }
    public decimal? EstimatedValue { get; set; }
    public Guid? AssignedToId { get; set; }
    public DateTime? NextFollowUpDate { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }
}

public class ConvertLeadDto
{
    public bool CreateOpportunity { get; set; } = true;

    [StringLength(200)]
    public string? OpportunityName { get; set; }

    public decimal? OpportunityAmount { get; set; }
}

#endregion

// ═══════════════════════════════════════════════════════════════════════════
//  OPPORTUNITY DTOs
// ═══════════════════════════════════════════════════════════════════════════

#region Opportunity DTOs

public class OpportunitySummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? StageDefinitionId { get; set; }
    public string Stage { get; set; } = string.Empty;
    public int Probability { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime ExpectedCloseDate { get; set; }
    public DateTime? ActualCloseDate { get; set; }
    public string? CustomerName { get; set; }
    public string? LeadName { get; set; }
    public string? AssignedToName { get; set; }
    public string OpportunityType { get; set; } = "New Business";
    public string LeadSource { get; set; } = "Unknown";
    public int QuoteCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class OpportunityDetailDto : OpportunitySummaryDto
{
    public string? Description { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? LeadId { get; set; }
    public Guid? AssignedToId { get; set; }
    public string? Competitors { get; set; }
    public string? Notes { get; set; }
    public string? LossReason { get; set; }
    public List<QuoteSummaryDto> Quotes { get; set; } = new();
    public List<ActivitySummaryDto> Activities { get; set; } = new();
}

public class CreateOpportunityDto
{
    [Required] [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    public Guid? CustomerId { get; set; }
    public Guid? LeadId { get; set; }
    public Guid? StageDefinitionId { get; set; }
    public string? Stage { get; set; }

    [Range(0, 100)]
    public int Probability { get; set; } = 10;

    [Required]
    public decimal Amount { get; set; }

    public string? Currency { get; set; } = "USD";

    [Required]
    public DateTime ExpectedCloseDate { get; set; }

    public string? LeadSource { get; set; } = "Unknown";
    public string? OpportunityType { get; set; } = "New Business";
    public Guid? AssignedToId { get; set; }

    [StringLength(500)]
    public string? Competitors { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }
}

public class UpdateOpportunityDto
{
    [StringLength(200)]
    public string? Name { get; set; }

    [StringLength(2000)]
    public string? Description { get; set; }

    public Guid? CustomerId { get; set; }
    public Guid? StageDefinitionId { get; set; }
    public string? Stage { get; set; }

    [Range(0, 100)]
    public int? Probability { get; set; }

    public decimal? Amount { get; set; }
    public DateTime? ExpectedCloseDate { get; set; }
    public string? LeadSource { get; set; }
    public string? OpportunityType { get; set; }
    public Guid? AssignedToId { get; set; }

    [StringLength(500)]
    public string? Competitors { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }
}

public class CloseOpportunityDto
{
    [Required]
    public bool Won { get; set; }

    [StringLength(2000)]
    public string? LossReason { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }
}

#endregion

// ═══════════════════════════════════════════════════════════════════════════
//  QUOTE DTOs
// ═══════════════════════════════════════════════════════════════════════════

#region Quote DTOs

public class QuoteSummaryDto
{
    public Guid Id { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public string QuoteName { get; set; } = string.Empty;
    public string QuoteStatus { get; set; } = "Draft";
    public Guid OpportunityId { get; set; }
    public string? OpportunityName { get; set; }
    public string? CustomerName { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal ExchangeRate { get; set; } = 1.0m;
    public DateTime ValidUntil { get; set; }
    public DateTime? SentDate { get; set; }
    public DateTime? AcceptedDate { get; set; }
    public int LineCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class QuoteDetailDto : QuoteSummaryDto
{
    public Guid? CustomerId { get; set; }
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal ShippingAmount { get; set; }
    public string? Proposal { get; set; }
    public Guid? ConvertedInvoiceId { get; set; }
    public string? ConvertedInvoiceNumber { get; set; }
    public Guid? TaxGroupId { get; set; }
    public decimal BaseCurrencyAmount { get; set; }
    public List<QuoteLineItemDto> LineItems { get; set; } = new();
}

public class QuoteLineItemDto
{
    public Guid Id { get; set; }
    public Guid QuoteId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public string? ProductCode { get; set; }
    public string? Unit { get; set; }
    public decimal DiscountPercentage { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public string? TaxCode { get; set; }
    public Guid? TaxGroupId { get; set; }
}

public class CreateQuoteDto
{
    [Required]
    public Guid OpportunityId { get; set; }

    public Guid? CustomerId { get; set; }

    [Required] [StringLength(200)]
    public string QuoteName { get; set; } = string.Empty;

    [Required]
    public DateTime ValidUntil { get; set; }

    public decimal? ShippingAmount { get; set; }

    [StringLength(2000)]
    public string? Proposal { get; set; }

    public string Currency { get; set; } = "USD";
    public decimal ExchangeRate { get; set; } = 1.0m;
    public Guid? TaxGroupId { get; set; }

    [Required]
    public List<CreateQuoteLineItemDto> LineItems { get; set; } = new();
}

public class CreateQuoteLineItemDto
{
    [Required] [StringLength(200)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public decimal Quantity { get; set; } = 1;

    [Required]
    public decimal UnitPrice { get; set; }

    [StringLength(50)]
    public string? ProductCode { get; set; }

    [StringLength(50)]
    public string? Unit { get; set; }

    public decimal? DiscountPercentage { get; set; }
    public decimal? TaxAmount { get; set; }

    [StringLength(50)]
    public string? TaxCode { get; set; }
    public Guid? TaxGroupId { get; set; }
}

public class UpdateQuoteDto
{
    [StringLength(200)]
    public string? QuoteName { get; set; }

    public DateTime? ValidUntil { get; set; }
    public decimal? ShippingAmount { get; set; }

    [StringLength(2000)]
    public string? Proposal { get; set; }

    public List<CreateQuoteLineItemDto>? LineItems { get; set; }
}

#endregion

// ═══════════════════════════════════════════════════════════════════════════
//  ACTIVITY DTOs
// ═══════════════════════════════════════════════════════════════════════════

#region Activity DTOs

public class ActivitySummaryDto
{
    public Guid Id { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string ActivityType { get; set; } = "Call";
    public DateTime ActivityDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string ActivityStatus { get; set; } = "Planned";
    public int Priority { get; set; }
    public string? AssignedToName { get; set; }
    public string? LeadName { get; set; }
    public string? CustomerName { get; set; }
    public string? OpportunityName { get; set; }
    public string? Outcome { get; set; }
    public bool RequiresFollowUp { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ActivityDetailDto : ActivitySummaryDto
{
    public string? Description { get; set; }
    public int? Duration { get; set; }
    public Guid? AssignedToId { get; set; }
    public Guid? LeadId { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? OpportunityId { get; set; }
    public string? Location { get; set; }
    public string? Attendees { get; set; }
    public string? Notes { get; set; }
    public DateTime? NextFollowUpDate { get; set; }
}

public class CreateActivityDto
{
    [Required] [StringLength(200)]
    public string Subject { get; set; } = string.Empty;

    public string ActivityType { get; set; } = "Call";

    [StringLength(2000)]
    public string? Description { get; set; }

    public DateTime? ActivityDate { get; set; }
    public DateTime? DueDate { get; set; }

    [Range(1, 4)]
    public int Priority { get; set; } = 2;

    public int? Duration { get; set; }
    public Guid? AssignedToId { get; set; }
    public Guid? LeadId { get; set; }
    public Guid? CustomerId { get; set; }
    public Guid? OpportunityId { get; set; }

    [StringLength(200)]
    public string? Location { get; set; }

    [StringLength(1000)]
    public string? Attendees { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }
}

public class UpdateActivityDto
{
    [StringLength(200)]
    public string? Subject { get; set; }

    public string? ActivityType { get; set; }

    [StringLength(2000)]
    public string? Description { get; set; }

    public DateTime? ActivityDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string? ActivityStatus { get; set; }

    [Range(1, 4)]
    public int? Priority { get; set; }

    public int? Duration { get; set; }
    public Guid? AssignedToId { get; set; }

    [StringLength(200)]
    public string? Location { get; set; }

    [StringLength(1000)]
    public string? Attendees { get; set; }

    [StringLength(50)]
    public string? Outcome { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }

    public bool? RequiresFollowUp { get; set; }
    public DateTime? NextFollowUpDate { get; set; }
}

#endregion

// ═══════════════════════════════════════════════════════════════════════════
//  CAMPAIGN DTOs
// ═══════════════════════════════════════════════════════════════════════════

#region Campaign DTOs

public class CampaignSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CampaignType { get; set; } = "Email";
    public string CampaignStatus { get; set; } = "Planning";
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal Budget { get; set; }
    public decimal ActualCost { get; set; }
    public decimal ExpectedRevenue { get; set; }
    public decimal ActualRevenue { get; set; }
    public int TargetAudience { get; set; }
    public int ResponseCount { get; set; }
    public decimal ResponseRate { get; set; }
    public int LeadsGenerated { get; set; }
    public int MemberCount { get; set; }
    public string? ManagerName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CampaignDetailDto : CampaignSummaryDto
{
    public string? Description { get; set; }
    public Guid? ManagerId { get; set; }
    public int ActualAudience { get; set; }
    public int OpportunitiesGenerated { get; set; }
    public string? Notes { get; set; }
    public List<CampaignMemberDto> Members { get; set; } = new();
}

public class CampaignMemberDto
{
    public Guid Id { get; set; }
    public Guid CampaignId { get; set; }
    public Guid? LeadId { get; set; }
    public string? LeadName { get; set; }
    public Guid? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string MemberStatus { get; set; } = "Active";
    public DateTime DateAdded { get; set; }
    public DateTime? ResponseDate { get; set; }
    public string? ResponseType { get; set; }
    public string? Notes { get; set; }
}

public class CreateCampaignDto
{
    [Required] [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    public string CampaignType { get; set; } = "Email";

    [StringLength(2000)]
    public string? Description { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }
    public decimal Budget { get; set; }
    public decimal ExpectedRevenue { get; set; }
    public Guid? ManagerId { get; set; }
    public int TargetAudience { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }
}

public class UpdateCampaignDto
{
    [StringLength(200)]
    public string? Name { get; set; }

    public string? CampaignType { get; set; }

    [StringLength(2000)]
    public string? Description { get; set; }

    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal? Budget { get; set; }
    public decimal? ActualCost { get; set; }
    public decimal? ExpectedRevenue { get; set; }
    public decimal? ActualRevenue { get; set; }
    public Guid? ManagerId { get; set; }
    public int? TargetAudience { get; set; }
    public int? ActualAudience { get; set; }
    public int? ResponseCount { get; set; }
    public int? LeadsGenerated { get; set; }
    public int? OpportunitiesGenerated { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }
}

public class AddCampaignMemberDto
{
    public Guid? LeadId { get; set; }
    public Guid? CustomerId { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }
}

#endregion

// ═══════════════════════════════════════════════════════════════════════════
//  PRODUCT CATALOG DTOs
// ═══════════════════════════════════════════════════════════════════════════

#region Product Catalog DTOs

public class ProductSummaryDto
{
    public Guid Id { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string ProductType { get; set; } = "Product";
    public string Category { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public decimal ListPrice { get; set; }
    public decimal CostPrice { get; set; }
    public decimal Margin { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ProductDetailDto : ProductSummaryDto
{
    public string? Description { get; set; }
    public string? Unit { get; set; }
    public string? TaxCode { get; set; }
    public string? Vendor { get; set; }
    public string? Specifications { get; set; }
}

public class CreateProductDto
{
    [Required] [StringLength(100)]
    public string ProductCode { get; set; } = string.Empty;

    [Required] [StringLength(200)]
    public string ProductName { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    public string ProductType { get; set; } = "Product";

    [Required] [StringLength(100)]
    public string Category { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Brand { get; set; }

    [StringLength(50)]
    public string? Unit { get; set; }

    [Required]
    public decimal ListPrice { get; set; }

    public decimal CostPrice { get; set; }

    [StringLength(50)]
    public string? TaxCode { get; set; }

    [StringLength(100)]
    public string? Vendor { get; set; }

    [StringLength(2000)]
    public string? Specifications { get; set; }
}

public class UpdateProductDto
{
    [StringLength(200)]
    public string? ProductName { get; set; }

    [StringLength(2000)]
    public string? Description { get; set; }

    public string? ProductType { get; set; }

    [StringLength(100)]
    public string? Category { get; set; }

    [StringLength(100)]
    public string? Brand { get; set; }

    [StringLength(50)]
    public string? Unit { get; set; }

    public decimal? ListPrice { get; set; }
    public decimal? CostPrice { get; set; }
    public bool? IsActive { get; set; }

    [StringLength(50)]
    public string? TaxCode { get; set; }

    [StringLength(100)]
    public string? Vendor { get; set; }

    [StringLength(2000)]
    public string? Specifications { get; set; }
}

#endregion
