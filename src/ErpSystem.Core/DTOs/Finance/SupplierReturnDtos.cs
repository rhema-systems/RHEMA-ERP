namespace ErpSystem.Core.DTOs.Finance;

public class SupplierReturnDto
{
    public Guid Id { get; set; }
    public string ReturnNumber { get; set; } = string.Empty;
    public Guid VendorId { get; set; }
    public string VendorName { get; set; } = string.Empty;
    public Guid? OriginalVendorInvoiceId { get; set; }
    public Guid? OriginalFinancePurchaseOrderReceiptId { get; set; }
    public LinkedVendorInvoiceDto? OriginalVendorInvoice { get; set; }
    public LinkedFinancePurchaseOrderReceiptDto? OriginalFinancePurchaseOrderReceipt { get; set; }
    public DateTime ReturnDate { get; set; }
    public string? Reason { get; set; }
    public string CurrencyCode { get; set; } = "GHS";
    public decimal ExchangeRate { get; set; } = 1m;
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal BaseCurrencyAmount { get; set; }
    public int Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public SupplierDebitNoteDto? DebitNote { get; set; }
    public List<SupplierReturnLineItemDto> LineItems { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

public class SupplierReturnLineItemDto
{
    public Guid Id { get; set; }
    public Guid? OriginalVendorInvoiceLineItemId { get; set; }
    public Guid? OriginalFinancePurchaseOrderItemId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal QuantityReturned { get; set; }
    public decimal UnitPrice { get; set; }
    public Guid? TaxGroupId { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountPercentage { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal LineTotal { get; set; }
}

public class CreateSupplierReturnDto
{
    public string? ReturnNumber { get; set; }
    public Guid VendorId { get; set; }
    public string? VendorName { get; set; }
    public Guid? OriginalVendorInvoiceId { get; set; }
    public Guid? OriginalFinancePurchaseOrderReceiptId { get; set; }
    public DateTime ReturnDate { get; set; }
    public string? Reason { get; set; }
    public string CurrencyCode { get; set; } = "GHS";
    public decimal ExchangeRate { get; set; } = 1m;
    public List<CreateSupplierReturnLineItemDto> Lines { get; set; } = new();
}

public class CreateSupplierReturnLineItemDto
{
    public Guid? OriginalVendorInvoiceLineItemId { get; set; }
    public Guid? OriginalFinancePurchaseOrderItemId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal QuantityReturned { get; set; }
    public decimal UnitPrice { get; set; }
    public Guid? TaxGroupId { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountPercentage { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal LineTotal { get; set; }
}

public class SupplierDebitNoteDto
{
    public Guid Id { get; set; }
    public string DebitNoteNumber { get; set; } = string.Empty;
    public string? SupplierCreditNoteReference { get; set; }
    public Guid VendorId { get; set; }
    /// <summary>Canonical AP Supplier identity paired with the selected Business Partner.</summary>
    public Guid? SupplierId { get; set; }
    public string VendorName { get; set; } = string.Empty;
    public Guid? SupplierReturnId { get; set; }
    public Guid? OriginalVendorInvoiceId { get; set; }
    public string? OriginalVendorInvoiceNumber { get; set; }
    public DateTime DebitNoteDate { get; set; }
    public string? Reason { get; set; }
    public string? Notes { get; set; }
    public string CurrencyCode { get; set; } = "GHS";
    public decimal ExchangeRate { get; set; }
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal BaseCurrencyAmount { get; set; }
    public decimal AppliedAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public string ApplicationStatus { get; set; } = "Unapplied";
    public Guid? JournalEntryId { get; set; }
    public Guid? PostingEventId { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? SubmittedById { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? RejectedById { get; set; }
    public DateTime? RejectedAt { get; set; }
    public string? RejectionReason { get; set; }
    public string ApprovalSource { get; set; } = string.Empty;
    public Guid? ReversalJournalEntryId { get; set; }
    public Guid? ReversalPostingEventId { get; set; }
    public DateTime? ReversedAt { get; set; }
    public Guid? ReversedById { get; set; }
    public string? ReversalReason { get; set; }
    public int Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public List<SupplierDebitNoteLineItemDto> LineItems { get; set; } = new();
    public List<SupplierDebitNoteApplicationDto> Applications { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class SupplierDebitNoteLineItemDto
{
    public Guid Id { get; set; }
    public Guid? OriginalVendorInvoiceLineItemId { get; set; }
    public Guid? OriginalFinancePurchaseOrderItemId { get; set; }
    public Guid? GLAccountId { get; set; }
    public Guid? ResolvedCreditAccountId { get; set; }
    public Guid? OriginalAccountTransactionId { get; set; }
    public string LineItemType { get; set; } = "Expense";
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public Guid? TaxGroupId { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountPercentage { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal LineTotal { get; set; }
}

public sealed class SupplierDebitNoteApplicationDto
{
    public Guid Id { get; set; }
    public Guid SupplierDebitNoteId { get; set; }
    public string DebitNoteNumber { get; set; } = string.Empty;
    public string? SupplierCreditNoteReference { get; set; }
    public Guid VendorPaymentId { get; set; }
    public string PaymentNumber { get; set; } = string.Empty;
    public Guid VendorInvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public decimal ApplicationAmount { get; set; }
    public decimal FunctionalAmount { get; set; }
    public string CurrencyCode { get; set; } = "GHS";
    public decimal ExchangeRate { get; set; }
    public DateTime ApplicationDate { get; set; }
    public string? Notes { get; set; }
    public bool IsReversal { get; set; }
    public Guid? OriginalApplicationId { get; set; }
    public Guid? PaymentPostingEventId { get; set; }
    public Guid? PaymentJournalEntryId { get; set; }
    public DateTime? AppliedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}

public class CreateSupplierDebitNoteDto
{
    public Guid VendorId { get; set; }
    public Guid? OriginalVendorInvoiceId { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(100)]
    public string? SupplierCreditNoteReference { get; set; }

    public DateTime DebitNoteDate { get; set; }

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MaxLength(500)]
    public string Reason { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.MaxLength(1000)]
    public string? Notes { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(3)]
    public string CurrencyCode { get; set; } = "GHS";

    public decimal ExchangeRate { get; set; } = 1m;
    public List<CreateSupplierDebitNoteLineItemDto> Lines { get; set; } = new();
}

public sealed class UpdateSupplierDebitNoteDto : CreateSupplierDebitNoteDto
{
    [System.ComponentModel.DataAnnotations.Required]
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class CreateSupplierDebitNoteLineItemDto
{
    public Guid? OriginalVendorInvoiceLineItemId { get; set; }
    public Guid? GLAccountId { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(20)]
    public string LineItemType { get; set; } = "Expense";

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Range(0.0001, double.MaxValue)]
    public decimal Quantity { get; set; }

    [System.ComponentModel.DataAnnotations.Range(0.01, double.MaxValue)]
    public decimal UnitPrice { get; set; }

    public Guid? TaxGroupId { get; set; }
    public decimal TaxRate { get; set; }
    public decimal? TaxAmount { get; set; }
    public decimal DiscountPercentage { get; set; }
    public decimal? DiscountAmount { get; set; }
    public decimal? LineTotal { get; set; }
}

public sealed class SupplierDebitNoteApprovalDto
{
    public bool Approve { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(1000)]
    public string? Comments { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(1000)]
    public string? RejectionReason { get; set; }
}

public sealed class ReverseSupplierDebitNoteDto
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.MaxLength(1000)]
    public string Reason { get; set; } = string.Empty;
    public DateTime? ReversalDate { get; set; }
}

public sealed class SupplierDebitNoteQueryDto
{
    public Guid? VendorId { get; set; }
    public Guid? SupplierId { get; set; }
    public Guid? OriginalVendorInvoiceId { get; set; }
    public ErpSystem.Core.Entities.Finance.SupplierDebitNoteStatus? Status { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? Search { get; set; }
}

/// <summary>
/// Finance-owned durable pairing between the shared Business Partner master and the legacy AP
/// Supplier master. Other module owners keep control of both source records; Finance owns only
/// this settlement identity and never writes their operational state.
/// </summary>
public sealed class ApSupplierIdentityDto
{
    public Guid BusinessPartnerId { get; set; }
    public Guid SupplierId { get; set; }
    public string PartnerCode { get; set; } = string.Empty;
    public string SupplierCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsVerified { get; set; }
}

public sealed class SupplierDebitNoteApplicationCreateDto
{
    [System.ComponentModel.DataAnnotations.Required]
    public Guid SupplierDebitNoteId { get; set; }

    [System.ComponentModel.DataAnnotations.Required]
    public Guid VendorInvoiceId { get; set; }

    [System.ComponentModel.DataAnnotations.Range(0.01, double.MaxValue)]
    public decimal ApplicationAmount { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(500)]
    public string? Notes { get; set; }
}

public sealed class SupplierDebitNoteApplicationResultDto
{
    public Guid PaymentId { get; set; }
    public decimal TotalSupplierCreditsApplied { get; set; }
    public List<SupplierDebitNoteApplicationDto> Applications { get; set; } = new();
}

public class LinkedVendorInvoiceDto
{
    public Guid Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
}

public class LinkedFinancePurchaseOrderReceiptDto
{
    public Guid Id { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
}
