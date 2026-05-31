using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Interfaces;

namespace ErpSystem.Core.Entities.Sales;

/// <summary>
/// Lead entity for potential customers
/// </summary>
public class Lead : BusinessEntity
{
    [Required]
    [StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    [NotMapped]
    public string FullName => $"{FirstName} {LastName}";

    [StringLength(100)]
    public string? CompanyName { get; set; }

    [StringLength(100)]
    public string? JobTitle { get; set; }

    [StringLength(100)]
    [EmailAddress]
    public string? Email { get; set; }

    [StringLength(20)]
    public string? Phone { get; set; }

    [StringLength(20)]
    public string? Mobile { get; set; }

    // Address
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

    // Lead qualification
    [StringLength(50)]
    public string LeadSource { get; set; } = "Unknown"; // Website, Referral, Cold Call, Social Media

    [StringLength(50)]
    public string LeadStatus { get; set; } = "New"; // New, Contacted, Qualified, Unqualified, Converted

    [Range(0, 100)]
    public int QualificationScore { get; set; } = 0;

    [Column(TypeName = "decimal(18,2)")]
    public decimal EstimatedValue { get; set; }

    public DateTime? LastContactDate { get; set; }
    public DateTime? NextFollowUpDate { get; set; }

    // Assignment
    public Guid? AssignedToId { get; set; }
    public virtual ApplicationUser? AssignedTo { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }

    // Conversion
    public Guid? ConvertedCustomerId { get; set; }
    public virtual Customer? ConvertedCustomer { get; set; }

    public DateTime? ConvertedDate { get; set; }

    // Multi-tenant
    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;

    // Navigation properties
    public virtual ICollection<Activity> Activities { get; set; } = new List<Activity>();
    public virtual ICollection<Opportunity> Opportunities { get; set; } = new List<Opportunity>();
}

/// <summary>
/// Opportunity entity for sales deals
/// </summary>
public class Opportunity : BusinessEntity
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    // Customer/Lead association
    public Guid? CustomerId { get; set; }
    public virtual Customer? Customer { get; set; }

    public Guid? LeadId { get; set; }
    public virtual Lead? Lead { get; set; }

    // Opportunity details
    [StringLength(50)]
    public string Stage { get; set; } = "Prospecting"; // Prospecting, Qualification, Proposal, Negotiation, Closed Won, Closed Lost

    [Range(0, 100)]
    public int Probability { get; set; } = 10; // Win probability percentage

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [StringLength(3)]
    public string Currency { get; set; } = "USD";

    public DateTime ExpectedCloseDate { get; set; }
    public DateTime? ActualCloseDate { get; set; }

    [StringLength(50)]
    public string LeadSource { get; set; } = "Unknown";

    [StringLength(50)]
    public string OpportunityType { get; set; } = "New Business"; // New Business, Existing Customer, Renewal

    // Assignment
    public Guid? AssignedToId { get; set; }
    public virtual ApplicationUser? AssignedTo { get; set; }

    // Competition
    [StringLength(500)]
    public string? Competitors { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }

    [StringLength(2000)]
    public string? LossReason { get; set; }

    // Multi-tenant
    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;

    // Navigation properties
    public virtual ICollection<Activity> Activities { get; set; } = new List<Activity>();
    public virtual ICollection<Quote> Quotes { get; set; } = new List<Quote>();
}

/// <summary>
/// Quote/Proposal entity
/// </summary>
public class Quote : DocumentEntity
{
    public Guid OpportunityId { get; set; }
    public virtual Opportunity Opportunity { get; set; } = null!;

    public Guid? CustomerId { get; set; }
    public virtual Customer? Customer { get; set; }

    [StringLength(200)]
    public string QuoteName { get; set; } = string.Empty;

    public DateTime ValidUntil { get; set; }

    [StringLength(50)]
    public string QuoteStatus { get; set; } = "Draft"; // Draft, Sent, Accepted, Rejected, Expired

    [Column(TypeName = "decimal(18,2)")]
    public decimal SubTotal { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ShippingAmount { get; set; }

    public DateTime? SentDate { get; set; }
    public DateTime? AcceptedDate { get; set; }

    // Conversion to invoice
    public Guid? ConvertedInvoiceId { get; set; }
    public virtual Invoice? ConvertedInvoice { get; set; }

    public Guid? TaxGroupId { get; set; }
    [ForeignKey(nameof(TaxGroupId))]
    public virtual TaxGroup? TaxGroup { get; set; }

    [StringLength(2000)]
    public string? Proposal { get; set; }

    // Multi-tenant
    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;

    // Navigation properties
    public virtual ICollection<QuoteLineItem> LineItems { get; set; } = new List<QuoteLineItem>();
}

/// <summary>
/// Quote line items
/// </summary>
public class QuoteLineItem : BaseEntity
{
    public Guid QuoteId { get; set; }
    public virtual Quote Quote { get; set; } = null!;

    [Required]
    [StringLength(200)]
    public string Description { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,4)")]
    public decimal Quantity { get; set; } = 1;

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal LineTotal => Quantity * UnitPrice;

    [StringLength(50)]
    public string? ProductCode { get; set; }

    [StringLength(50)]
    public string? Unit { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal DiscountPercentage { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TaxAmount { get; set; }

    [StringLength(50)]
    public string? TaxCode { get; set; }

    public Guid? TaxGroupId { get; set; }
    [ForeignKey(nameof(TaxGroupId))]
    public virtual TaxGroup? TaxGroup { get; set; }

    // Multi-tenant
    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;
}

/// <summary>
/// Activity entity for CRM activities (calls, meetings, emails, etc.)
/// </summary>
public class Activity : BusinessEntity
{
    [Required]
    [StringLength(200)]
    public string Subject { get; set; } = string.Empty;

    [StringLength(50)]
    public string ActivityType { get; set; } = "Call"; // Call, Meeting, Email, Task, Note

    [StringLength(2000)]
    public string? Description { get; set; }

    public DateTime ActivityDate { get; set; } = DateTime.UtcNow;
    public DateTime? DueDate { get; set; }

    [StringLength(50)]
    public string ActivityStatus { get; set; } = "Planned"; // Planned, In Progress, Completed, Cancelled

    [Range(1, 4)]
    public new int Priority { get; set; } = 2; // 1 = High, 2 = Medium, 3 = Low, 4 = Very Low

    // Duration in minutes
    public int? Duration { get; set; }

    // Assignment
    public Guid? AssignedToId { get; set; }
    public virtual ApplicationUser? AssignedTo { get; set; }

    // Related entities
    public Guid? LeadId { get; set; }
    public virtual Lead? Lead { get; set; }

    public Guid? CustomerId { get; set; }
    public virtual Customer? Customer { get; set; }

    public Guid? OpportunityId { get; set; }
    public virtual Opportunity? Opportunity { get; set; }

    // Location (for meetings)
    [StringLength(200)]
    public string? Location { get; set; }

    // Meeting attendees (JSON array)
    [StringLength(1000)]
    public string? Attendees { get; set; }

    // Outcome
    [StringLength(50)]
    public string? Outcome { get; set; } // Successful, No Answer, Left Message, Reschedule, etc.

    [StringLength(2000)]
    public string? Notes { get; set; }

    // Follow-up
    public bool RequiresFollowUp { get; set; }
    public DateTime? NextFollowUpDate { get; set; }

    // Multi-tenant
    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;
}

/// <summary>
/// Sales Campaign entity for marketing campaigns
/// </summary>
public class Campaign : BusinessEntity
{
    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(50)]
    public string CampaignType { get; set; } = "Email"; // Email, Social Media, Print, Radio, TV, Online, Event

    [StringLength(2000)]
    public string? Description { get; set; }

    public DateTime StartDate { get; set; } = DateTime.UtcNow;
    public DateTime? EndDate { get; set; }

    [StringLength(50)]
    public string CampaignStatus { get; set; } = "Planning"; // Planning, Active, Paused, Completed, Cancelled

    [Column(TypeName = "decimal(18,2)")]
    public decimal Budget { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ActualCost { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ExpectedRevenue { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ActualRevenue { get; set; }

    // Campaign manager
    public Guid? ManagerId { get; set; }
    public virtual ApplicationUser? Manager { get; set; }

    // Metrics
    public int TargetAudience { get; set; }
    public int ActualAudience { get; set; }
    public int ResponseCount { get; set; }
    public int LeadsGenerated { get; set; }
    public int OpportunitiesGenerated { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal ResponseRate => ActualAudience > 0 ? (decimal)ResponseCount / ActualAudience * 100 : 0;

    [StringLength(2000)]
    public string? Notes { get; set; }

    // Multi-tenant
    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;

    // Navigation properties
    public virtual ICollection<CampaignMember> CampaignMembers { get; set; } = new List<CampaignMember>();
}

/// <summary>
/// Campaign member association (leads/customers in campaigns)
/// </summary>
public class CampaignMember : BaseEntity
{
    public Guid CampaignId { get; set; }
    public virtual Campaign Campaign { get; set; } = null!;

    public Guid? LeadId { get; set; }
    public virtual Lead? Lead { get; set; }

    public Guid? CustomerId { get; set; }
    public virtual Customer? Customer { get; set; }

    [StringLength(50)]
    public string MemberStatus { get; set; } = "Active"; // Active, Responded, Unsubscribed, Bounced

    public DateTime DateAdded { get; set; } = DateTime.UtcNow;
    public DateTime? ResponseDate { get; set; }

    [StringLength(50)]
    public string? ResponseType { get; set; } // Opened, Clicked, Replied, Unsubscribed

    [StringLength(1000)]
    public string? Notes { get; set; }

    // Multi-tenant
    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;
}

/// <summary>
/// Product/Service catalog for sales
/// </summary>
public class Product : BusinessEntity
{
    [Required]
    [StringLength(100)]
    public string ProductCode { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string ProductName { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    [StringLength(50)]
    public string ProductType { get; set; } = "Product"; // Product, Service

    [StringLength(100)]
    public string Category { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Brand { get; set; }

    [StringLength(50)]
    public string? Unit { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ListPrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CostPrice { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal Margin => ListPrice > 0 ? (ListPrice - CostPrice) / ListPrice * 100 : 0;

    public new bool IsActive { get; set; } = true;

    [StringLength(50)]
    public string? TaxCode { get; set; }

    [StringLength(100)]
    public string? Vendor { get; set; }

    [StringLength(2000)]
    public string? Specifications { get; set; }

    // Multi-tenant
    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;
}
