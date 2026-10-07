using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Sales;

namespace ErpSystem.Core.Entities.Ehc;

/// <summary>
/// Mutable Sales state for a public property enquiry. The immutable property/contact snapshot remains
/// on EhcTicket.PropertyListingContextJson; this row records the governed qualification, reservation,
/// deposit and eventual Business Partner relationship.
/// </summary>
[Table("EhcPropertyEnquiryProspects")]
public sealed class EhcPropertyEnquiryProspect : TenantEntity
{
    public Guid TicketId { get; set; }
    public EhcTicket Ticket { get; set; } = null!;

    public Guid? LeadId { get; set; }
    public Lead? Lead { get; set; }

    public Guid? OpportunityId { get; set; }
    public Opportunity? Opportunity { get; set; }

    public Guid? SalesAllocationId { get; set; }
    public SalesAllocation? SalesAllocation { get; set; }

    public Guid? BusinessPartnerId { get; set; }
    public BusinessPartner? BusinessPartner { get; set; }

    [Required, MaxLength(30)]
    public string Status { get; set; } = EhcPropertyProspectStatuses.New;

    public DateTime? QualifiedAt { get; set; }
    public Guid? QualifiedById { get; set; }

    public DateTime? BusinessPartnerLinkedAt { get; set; }
    public Guid? BusinessPartnerLinkedById { get; set; }

    [MaxLength(30)]
    public string DepositRequirementType { get; set; } = ProspectDepositRequirementTypes.Full;

    [Column(TypeName = "decimal(18,2)")]
    public decimal? FixedDepositAmount { get; set; }

    [Column(TypeName = "decimal(9,4)")]
    public decimal? DepositPercentage { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal AgreedAmount { get; set; }

    [Required, MaxLength(3)]
    public string Currency { get; set; } = "GHS";

    public ICollection<ProspectDepositReceipt> DepositReceipts { get; set; } = new List<ProspectDepositReceipt>();
}

/// <summary>Tenant/source policy controlling the payment threshold and Finance posting accounts.</summary>
[Table("EhcPropertyProspectDepositPolicies")]
public sealed class EhcPropertyProspectDepositPolicy : TenantEntity
{
    public Guid SalesSaleableSourceId { get; set; }
    public SalesSaleableSource SalesSaleableSource { get; set; } = null!;

    [Required, MaxLength(30)]
    public string RequirementType { get; set; } = ProspectDepositRequirementTypes.Full;

    [Column(TypeName = "decimal(18,2)")]
    public decimal? FixedAmount { get; set; }

    [Column(TypeName = "decimal(9,4)")]
    public decimal? Percentage { get; set; }

    public Guid DepositLiabilityAccountId { get; set; }
    public Guid? DefaultBankAccountId { get; set; }
    public Guid? DefaultLiquidityAccountId { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Cash received before a public prospect is eligible to become a customer Business Partner.
/// This is deliberately separate from CustomerPayment, whose canonical AR identity requires a BP.
/// </summary>
[Table("EhcProspectDepositReceipts")]
public sealed class ProspectDepositReceipt : TenantEntity
{
    public Guid ProspectId { get; set; }
    public EhcPropertyEnquiryProspect Prospect { get; set; } = null!;

    public Guid TicketId { get; set; }
    public Guid? LeadId { get; set; }
    public Guid OpportunityId { get; set; }
    public Guid? SalesAllocationId { get; set; }
    public Guid? BusinessPartnerId { get; set; }

    [Required, MaxLength(50)]
    public string ReceiptNumber { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Required, MaxLength(3)]
    public string Currency { get; set; } = "GHS";

    [Required, MaxLength(40)]
    public string PaymentMethod { get; set; } = "BankTransfer";

    [MaxLength(100)]
    public string? TransactionReference { get; set; }

    public DateTime ReceivedAt { get; set; }

    [Required, MaxLength(20)]
    public string Status { get; set; } = ProspectDepositReceiptStatuses.Pending;

    public DateTime? ClearedAt { get; set; }
    public Guid? ClearedById { get; set; }
    public DateTime? ReversedAt { get; set; }
    public Guid? ReversedById { get; set; }

    [MaxLength(1000)]
    public string? ReversalReason { get; set; }

    public Guid DepositLiabilityAccountId { get; set; }
    public Guid? BankAccountId { get; set; }
    public Guid? LiquidityAccountId { get; set; }

    public Guid? PostingEventId { get; set; }
    public Guid? JournalEntryId { get; set; }
    public Guid? ReversalPostingEventId { get; set; }
    public Guid? ReversalJournalEntryId { get; set; }

    /// <summary>
    /// Reclassification lineage. Cash is never posted again when the prospect becomes a BP; Finance
    /// transfers this liability to the customer-advance subledger and records those identifiers here.
    /// </summary>
    public Guid? CustomerAdvanceTransferPostingEventId { get; set; }
    public Guid? CustomerAdvanceTransferJournalEntryId { get; set; }
    public Guid? CustomerPaymentId { get; set; }
    public DateTime? TransferredToCustomerAdvanceAt { get; set; }
}

[Table("EhcPropertyEnquiryEmailAttempts")]
public sealed class EhcPropertyEnquiryEmailAttempt : TenantEntity
{
    public Guid TicketId { get; set; }
    public EhcTicket Ticket { get; set; } = null!;

    [Required, MaxLength(200)]
    public string Recipient { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Sender { get; set; }

    [Required, MaxLength(250)]
    public string Subject { get; set; } = string.Empty;

    [Required, MaxLength(64)]
    public string BodySha256 { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string Status { get; set; } = PropertyEnquiryEmailStatuses.Pending;

    public DateTime AttemptedAt { get; set; }
    public DateTime? SentAt { get; set; }

    [MaxLength(1000)]
    public string? FailureReason { get; set; }
}

public static class EhcPropertyProspectStatuses
{
    public const string New = "New";
    public const string Contacted = "Contacted";
    public const string Qualified = "Qualified";
    public const string Opportunity = "Opportunity";
    public const string CustomerPendingApproval = "CustomerPendingApproval";
    public const string Converted = "Converted";
    public const string Disqualified = "Disqualified";
}

public static class ProspectDepositRequirementTypes
{
    public const string Fixed = "Fixed";
    public const string Percentage = "Percentage";
    public const string Full = "Full";
}

public static class ProspectDepositReceiptStatuses
{
    public const string Pending = "Pending";
    public const string Cleared = "Cleared";
    public const string Reversed = "Reversed";
}

public static class PropertyEnquiryEmailStatuses
{
    public const string Pending = "Pending";
    public const string Sent = "Sent";
    public const string Failed = "Failed";
}
