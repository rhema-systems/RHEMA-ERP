using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Ehc;

public sealed record PropertyEnquiryProspectDto(
    Guid TicketId,
    Guid LeadId,
    Guid? OpportunityId,
    Guid? SalesAllocationId,
    Guid? BusinessPartnerId,
    string? BusinessPartnerCode,
    string? BusinessPartnerName,
    string Status,
    decimal AgreedAmount,
    string Currency,
    string DepositRequirementType,
    decimal RequiredDeposit,
    decimal ClearedDeposit,
    bool DepositThresholdMet,
    DateTime? QualifiedAt,
    DateTime? BusinessPartnerLinkedAt)
{
    public string? SalesAllocationStatus { get; init; }
    public DateTime? SalesAllocationReservedUntil { get; init; }
}

public sealed class QualifyPropertyEnquiryRequest
{
    [Range(1, 100)] public int QualificationScore { get; set; } = 40;
    [Range(0.01, 999999999999.99)] public decimal AgreedAmount { get; set; }
    [RegularExpression("^[A-Za-z]{3}$")] public string? Currency { get; set; }
    [StringLength(2000)] public string? Notes { get; set; }
}

public sealed class RecordPropertyEnquiryContactRequest
{
    [StringLength(2000)] public string? Notes { get; set; }
}

public sealed class DisqualifyPropertyEnquiryRequest
{
    [Required, StringLength(2000)] public string Reason { get; set; } = string.Empty;
}

public sealed class CreatePropertyEnquiryOpportunityRequest
{
    [Range(0.01, 999999999999.99)] public decimal Amount { get; set; }
    [RegularExpression("^[A-Za-z]{3}$")] public string? Currency { get; set; }
    public DateTime ExpectedCloseDate { get; set; }
    public bool ReserveProperty { get; set; } = true;
    [Range(1, 365)] public int ReservationDays { get; set; } = 14;
    [StringLength(2000)] public string? Notes { get; set; }
}

public sealed record PropertyEnquiryBusinessPartnerMatchDto(
    Guid Id, string PartnerCode, string PartnerName, string? Email, string? Phone,
    string? ApprovalStatus, bool IsActive, IReadOnlyList<string> MatchedOn);

public sealed class LinkPropertyEnquiryBusinessPartnerRequest
{
    [Required] public Guid BusinessPartnerId { get; set; }
}

public sealed class CreatePropertyEnquiryBusinessPartnerRequest
{
    [StringLength(200)] public string? PartnerName { get; set; }
    [EmailAddress, StringLength(200)] public string? Email { get; set; }
    [Phone, StringLength(50)] public string? Phone { get; set; }
    [StringLength(500)] public string? PhysicalAddress { get; set; }
    [StringLength(100)] public string? City { get; set; }
    [StringLength(100)] public string? Country { get; set; }
    [StringLength(20)] public string? PostalCode { get; set; }
}

public sealed class RecordProspectDepositRequest
{
    [Range(0.01, 999999999999.99)] public decimal Amount { get; set; }
    [Required, RegularExpression("^[A-Za-z]{3}$")] public string Currency { get; set; } = "GHS";
    [Required, StringLength(40)] public string PaymentMethod { get; set; } = "BankTransfer";
    [StringLength(100)] public string? TransactionReference { get; set; }
    public DateTime? ReceivedAt { get; set; }
}

public sealed class ClearProspectDepositRequest
{
    public DateTime? ClearedAt { get; set; }
}

public sealed class ReverseProspectDepositRequest
{
    [Required, StringLength(1000)] public string Reason { get; set; } = string.Empty;
    public DateTime? ReversalDate { get; set; }
}

public sealed record ProspectDepositReceiptDto(
    Guid Id, string ReceiptNumber, decimal Amount, string Currency, string PaymentMethod,
    string? TransactionReference, string Status, DateTime ReceivedAt, DateTime? ClearedAt,
    DateTime? ReversedAt, Guid? PostingEventId, Guid? JournalEntryId,
    Guid? CustomerAdvanceTransferPostingEventId, Guid? CustomerPaymentId);

public sealed class SendPropertyEnquiryEmailRequest
{
    [Required, StringLength(250)] public string Subject { get; set; } = string.Empty;
    [Required, StringLength(10000)] public string Body { get; set; } = string.Empty;
}

public sealed record PropertyEnquiryEmailResultDto(Guid AttemptId, bool Sent, string Recipient, string? FailureReason);

public sealed class UpsertPropertyProspectDepositPolicyRequest
{
    [Required] public Guid SalesSaleableSourceId { get; set; }
    [Required, RegularExpression("^(Fixed|Percentage|Full)$")] public string RequirementType { get; set; } = "Full";
    [Range(0.01, 999999999999.99)] public decimal? FixedAmount { get; set; }
    [Range(0.0001, 100)] public decimal? Percentage { get; set; }
    [Required] public Guid DepositLiabilityAccountId { get; set; }
    public Guid? DefaultBankAccountId { get; set; }
    public Guid? DefaultLiquidityAccountId { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed record PropertyProspectDepositPolicyDto(
    Guid Id,
    Guid SalesSaleableSourceId,
    string RequirementType,
    decimal? FixedAmount,
    decimal? Percentage,
    Guid DepositLiabilityAccountId,
    Guid? DefaultBankAccountId,
    Guid? DefaultLiquidityAccountId,
    bool IsActive);
