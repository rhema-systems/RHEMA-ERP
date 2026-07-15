using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using ErpSystem.Core.Entities.Finance;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.DTOs.Finance;

// ═══════════════════════════════════════════════════════════════════════════
//  VENDOR INVOICE DTOs
// ═══════════════════════════════════════════════════════════════════════════

#region Vendor Invoice

public class VendorInvoiceDto
{
    public Guid Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string? SupplierInvoiceNumber { get; set; }

    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;

    public Guid? PurchaseOrderId { get; set; }
    public string? PurchaseOrderNumber { get; set; }

    public DateTime InvoiceDate { get; set; }
    public DateTime? ReceivedDate { get; set; }
    public DateTime? DueDate { get; set; }

    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal BalanceAmount { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;
    public decimal ExchangeRate { get; set; } = 1.0m;
    public decimal BaseCurrencyAmount { get; set; }

    public int PaymentTermsDays { get; set; }
    public Guid? PaymentTermId { get; set; }

    // Early-payment discount
    public decimal EarlyPaymentDiscountPercentage { get; set; }
    public DateTime? EarlyPaymentDiscountDueDate { get; set; }
    public decimal EarlyPaymentDiscountAmount { get; set; }

    // Withholding tax
    public decimal WithholdingTaxRate { get; set; }
    public decimal WithholdingTaxAmount { get; set; }
    public Guid? WithholdingTaxId { get; set; }
    public Guid? WithholdingTaxAccountId { get; set; }
    public string? WithholdingCertificateNumber { get; set; }
    public DateTime? WithholdingCertificateDate { get; set; }

    // Matching
    public InvoiceMatchingType MatchingType { get; set; }
    public InvoiceMatchingStatus MatchingStatus { get; set; }
    public string? MatchingNotes { get; set; }

    // Status
    public VendorInvoiceStatus Status { get; set; }
    public string ApprovalStatus { get; set; } = "Draft";

    // GL
    public Guid? ExpenseAccountId { get; set; }
    public string? ExpenseAccountName { get; set; }
    public Guid? ApAccountId { get; set; }
    public string? ApAccountName { get; set; }
    public Guid? JournalEntryId { get; set; }

    public string? Notes { get; set; }
    public string? Reference { get; set; }
    public bool IsOpeningBalance { get; set; }

    public List<VendorInvoiceLineItemDto> LineItems { get; set; } = new();
    public List<VendorPaymentAllocationDto> PaymentAllocations { get; set; } = new();

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class VendorInvoiceCreateDto
{
    public string? SupplierInvoiceNumber { get; set; }

    [Required]
    public Guid SupplierId { get; set; }

    public Guid? PurchaseOrderId { get; set; }

    [Required]
    public DateTime InvoiceDate { get; set; }

    public DateTime? ReceivedDate { get; set; }
    public DateTime? DueDate { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;
    public decimal ExchangeRate { get; set; } = 1.0m;

    public int PaymentTermsDays { get; set; } = 30;
    public Guid? PaymentTermId { get; set; }

    // Early-payment discount
    public decimal EarlyPaymentDiscountPercentage { get; set; }
    public DateTime? EarlyPaymentDiscountDueDate { get; set; }

    // Withholding tax
    public decimal WithholdingTaxRate { get; set; }
    public Guid? WithholdingTaxId { get; set; }
    public Guid? WithholdingTaxAccountId { get; set; }
    public string? WithholdingCertificateNumber { get; set; }
    public DateTime? WithholdingCertificateDate { get; set; }

    // Matching
    public InvoiceMatchingType MatchingType { get; set; } = InvoiceMatchingType.None;

    // GL accounts
    public Guid? ExpenseAccountId { get; set; }
    public Guid? ApAccountId { get; set; }

    public string? Notes { get; set; }
    public string? Reference { get; set; }
    public bool IsOpeningBalance { get; set; }

    [Required]
    public List<VendorInvoiceLineItemCreateDto> LineItems { get; set; } = new();
}

public class VendorInvoiceUpdateDto
{
    [Required]
    public Guid Id { get; set; }

    public string? SupplierInvoiceNumber { get; set; }
    public Guid? PurchaseOrderId { get; set; }

    public DateTime InvoiceDate { get; set; }
    public DateTime? ReceivedDate { get; set; }
    public DateTime? DueDate { get; set; }

    public string CurrencyCode { get; set; } = "USD";
    public decimal ExchangeRate { get; set; } = 1.0m;

    public int PaymentTermsDays { get; set; } = 30;
    public Guid? PaymentTermId { get; set; }

    public decimal EarlyPaymentDiscountPercentage { get; set; }
    public DateTime? EarlyPaymentDiscountDueDate { get; set; }

    public decimal WithholdingTaxRate { get; set; }
    public Guid? WithholdingTaxId { get; set; }
    public Guid? WithholdingTaxAccountId { get; set; }
    public string? WithholdingCertificateNumber { get; set; }
    public DateTime? WithholdingCertificateDate { get; set; }

    public InvoiceMatchingType MatchingType { get; set; }

    public Guid? ExpenseAccountId { get; set; }
    public Guid? ApAccountId { get; set; }

    public string? Notes { get; set; }
    public string? Reference { get; set; }
    public bool IsOpeningBalance { get; set; }

    public List<VendorInvoiceLineItemCreateDto> LineItems { get; set; } = new();
}

public class VendorInvoiceQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public Guid? SupplierId { get; set; }
    public VendorInvoiceStatus? Status { get; set; }
    public string? ApprovalStatus { get; set; }
    public InvoiceMatchingStatus? MatchingStatus { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public DateTime? DueFromDate { get; set; }
    public DateTime? DueToDate { get; set; }
    public bool? OverdueOnly { get; set; }
    public bool? IsOpeningBalance { get; set; }
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; } = true;
}

public class VendorInvoiceLineItemDto
{
    public Guid Id { get; set; }
    public Guid VendorInvoiceId { get; set; }
    public string LineItemType { get; set; } = "Expense";
    public Guid? GLAccountId { get; set; }
    public string? GLAccountName { get; set; }
    public Guid? FixedAssetId { get; set; }
    public Guid? CapitalizationJournalEntryId { get; set; }
    public Guid? CapitalizationPostingEventId { get; set; }
    public DateTime? CapitalizedAt { get; set; }
    /// <summary>
    /// Legacy procurement PO line id only; finance PO/GRV lines use their receipt/invoice linkage instead.
    /// </summary>
    public Guid? PurchaseOrderItemId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public Guid? TaxGroupId { get; set; }
    public TaxTreatment TaxTreatment { get; set; } = TaxTreatment.Standard;
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public string? TaxCode { get; set; }
    public decimal DiscountPercentage { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? Unit { get; set; }
}

public class VendorInvoiceLineItemCreateDto
{
    public string LineItemType { get; set; } = "Expense";
    public Guid? GLAccountId { get; set; }
    public Guid? FixedAssetId { get; set; }
    /// <summary>
    /// Legacy procurement PO line id only; do not send FinancePurchaseOrderItem ids in this field.
    /// </summary>
    public Guid? PurchaseOrderItemId { get; set; }

    [Required]
    public string Description { get; set; } = string.Empty;

    public decimal Quantity { get; set; } = 1;

    [Required]
    public decimal UnitPrice { get; set; }

    public Guid? TaxGroupId { get; set; }
    public TaxTreatment TaxTreatment { get; set; } = TaxTreatment.Standard;
    public decimal TaxRate { get; set; }
    public string? TaxCode { get; set; }
    public decimal DiscountPercentage { get; set; }
    public string? Unit { get; set; }
}

public class InvoiceMatchingResultDto
{
    public Guid VendorInvoiceId { get; set; }
    public InvoiceMatchingType MatchingType { get; set; }
    public InvoiceMatchingStatus MatchingStatus { get; set; }
    public bool IsMatched { get; set; }
    public List<MatchingDiscrepancyDto> Discrepancies { get; set; } = new();
    public decimal InvoiceTotal { get; set; }
    public decimal? PurchaseOrderTotal { get; set; }
    public decimal? GoodsReceiptTotal { get; set; }
    public decimal TolerancePercentage { get; set; } = 1.0m;
}

public class MatchingDiscrepancyDto
{
    public string ItemDescription { get; set; } = string.Empty;
    public string DiscrepancyType { get; set; } = string.Empty; // Price, Quantity, Missing
    public decimal InvoiceValue { get; set; }
    public decimal? ExpectedValue { get; set; }
    public decimal Variance { get; set; }
    public decimal VariancePercentage { get; set; }
}

#endregion

// ═══════════════════════════════════════════════════════════════════════════
//  VENDOR PAYMENT DTOs
// ═══════════════════════════════════════════════════════════════════════════

#region Vendor Payment

public class VendorPaymentDto
{
    public Guid Id { get; set; }
    public string PaymentNumber { get; set; } = string.Empty;
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public DateTime PaymentDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AllocatedAmount { get; set; }
    public decimal UnallocatedAmount { get; set; }
    public VendorPaymentMethod PaymentMethod { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public decimal ExchangeRate { get; set; }
    public Guid? BankAccountId { get; set; }
    public string? BankAccountName { get; set; }
    public string? ChequeNumber { get; set; }
    public string? TransactionReference { get; set; }
    public decimal WithholdingTaxRate { get; set; }
    public decimal WithholdingTaxAmount { get; set; }
    public Guid? WithholdingTaxId { get; set; }
    public Guid? WithholdingTaxAccountId { get; set; }
    public string? WithholdingCertificateNumber { get; set; }
    public DateTime? WithholdingCertificateDate { get; set; }
    public decimal DiscountTaken { get; set; }
    public VendorPaymentStatus Status { get; set; }
    public Guid? PaymentBatchId { get; set; }
    public string? PaymentBatchNumber { get; set; }
    public Guid? JournalEntryId { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<VendorPaymentAllocationDto> Allocations { get; set; } = new();
}

public class VendorPaymentCreateDto
{
    [Required]
    public Guid SupplierId { get; set; }

    [Required]
    public DateTime PaymentDate { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal TotalAmount { get; set; }

    public VendorPaymentMethod PaymentMethod { get; set; } = VendorPaymentMethod.BankTransfer;

    public string CurrencyCode { get; set; } = "USD";
    public decimal ExchangeRate { get; set; } = 1.0m;

    public Guid? BankAccountId { get; set; }
    public string? ChequeNumber { get; set; }
    public string? TransactionReference { get; set; }

    public decimal WithholdingTaxRate { get; set; }
    public Guid? WithholdingTaxId { get; set; }
    public Guid? WithholdingTaxAccountId { get; set; }
    public string? WithholdingCertificateNumber { get; set; }
    public DateTime? WithholdingCertificateDate { get; set; }

    public string? Notes { get; set; }

    /// <summary>
    /// Optional: allocations to create immediately with the payment.
    /// </summary>
    public List<VendorPaymentAllocationCreateDto>? Allocations { get; set; }
}

public class VendorPaymentQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? SearchTerm { get; set; }
    public Guid? SupplierId { get; set; }
    public VendorPaymentStatus? Status { get; set; }
    public VendorPaymentMethod? PaymentMethod { get; set; }
    public Guid? PaymentBatchId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; } = true;
}

public class VendorPaymentAllocationDto
{
    public Guid Id { get; set; }
    public Guid VendorPaymentId { get; set; }
    public Guid VendorInvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public decimal AllocatedAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal WithholdingTaxAmount { get; set; }
    public DateTime AllocationDate { get; set; }
    public string? Notes { get; set; }
    public bool IsReversal { get; set; }
}

public class VendorPaymentAllocationCreateDto
{
    [Required]
    public Guid VendorInvoiceId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue)]
    public decimal AllocatedAmount { get; set; }

    public decimal DiscountAmount { get; set; }
    public decimal WithholdingTaxAmount { get; set; }
    public string? Notes { get; set; }
}

public class VendorPaymentAllocationResultDto
{
    public Guid PaymentId { get; set; }
    public decimal TotalAllocated { get; set; }
    public decimal RemainingUnallocated { get; set; }
    public List<VendorPaymentAllocationDto> Allocations { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class OutstandingVendorInvoiceDto
{
    public Guid InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string? SupplierInvoiceNumber { get; set; }
    public DateTime InvoiceDate { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal BalanceAmount { get; set; }
    public int DaysOverdue { get; set; }
    public decimal? EarlyPaymentDiscountPercentage { get; set; }
    public DateTime? EarlyPaymentDiscountDueDate { get; set; }
    public bool IsDiscountAvailable { get; set; }
    public decimal? DiscountAmount { get; set; }
}

public class EarlyPaymentDiscountResultDto
{
    public Guid InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public decimal InvoiceBalance { get; set; }
    public decimal DiscountPercentage { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal NetPayableAmount { get; set; }
    public DateTime DiscountDueDate { get; set; }
    public bool IsEligible { get; set; }
    public int DaysUntilExpiry { get; set; }
}

#endregion

// ═══════════════════════════════════════════════════════════════════════════
//  PAYMENT BATCH DTOs
// ═══════════════════════════════════════════════════════════════════════════

#region Payment Batch

public class PaymentBatchDto
{
    public Guid Id { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime BatchDate { get; set; }
    public DateTime? DueDateFrom { get; set; }
    public DateTime? DueDateTo { get; set; }
    public decimal TotalAmount { get; set; }
    public int PaymentCount { get; set; }
    public VendorPaymentMethod PaymentMethod { get; set; }
    public Guid? BankAccountId { get; set; }
    public string? BankAccountName { get; set; }
    public PaymentBatchStatus Status { get; set; }
    public DateTime? ApprovedDate { get; set; }
    public DateTime? ProcessedDate { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<PaymentBatchItemDto> Items { get; set; } = new();
}

public class PaymentBatchCreateDto
{
    public string? Description { get; set; }

    [Required]
    public DateTime BatchDate { get; set; }

    public DateTime? DueDateFrom { get; set; }
    public DateTime? DueDateTo { get; set; }

    public VendorPaymentMethod PaymentMethod { get; set; } = VendorPaymentMethod.BankTransfer;
    public Guid? BankAccountId { get; set; }

    public string? Notes { get; set; }

    /// <summary>
    /// IDs of approved vendor invoices to create payments for.
    /// </summary>
    [Required]
    public List<Guid> InvoiceIds { get; set; } = new();
}

public class PaymentBatchQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public PaymentBatchStatus? Status { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; } = true;
}

public class PaymentBatchItemDto
{
    public Guid Id { get; set; }
    public Guid VendorPaymentId { get; set; }
    public string PaymentNumber { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string ItemStatus { get; set; } = "Pending";
    public string? FailureReason { get; set; }
}

#endregion

// ═══════════════════════════════════════════════════════════════════════════
//  AP REPORT DTOs
// ═══════════════════════════════════════════════════════════════════════════

#region AP Reports

public class ApAgingReportDto
{
    public DateTime AsOfDate { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public bool UsesSettlementReadModel { get; set; }
    public decimal TotalOutstanding { get; set; }
    public decimal Current { get; set; }         // 0-30 days
    public decimal ThirtyDays { get; set; }      // 31-60 days
    public decimal SixtyDays { get; set; }       // 61-90 days
    public decimal NinetyPlusDays { get; set; }  // 90+ days
    public int TotalSuppliers { get; set; }
    public int TotalInvoices { get; set; }
    public List<SupplierAgingDetailDto> SupplierDetails { get; set; } = new();
    public List<SubledgerSettlementDiagnosticDto> Diagnostics { get; set; } = new();
}

public class SupplierAgingDetailDto
{
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string? SupplierCode { get; set; }
    public decimal TotalOutstanding { get; set; }
    public decimal Current { get; set; }
    public decimal ThirtyDays { get; set; }
    public decimal SixtyDays { get; set; }
    public decimal NinetyPlusDays { get; set; }
    public int InvoiceCount { get; set; }
    public DateTime? OldestInvoiceDate { get; set; }
    public List<ApAgingInvoiceDto> Invoices { get; set; } = new();
}

public class ApAgingInvoiceDto
{
    public Guid InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal SettledAmount { get; set; }
    public decimal CreditedAmount { get; set; }
    public decimal WithheldAmount { get; set; }
    public decimal BalanceAmount { get; set; }
    public Guid? SourcePostingEventId { get; set; }
    public Guid? SourceJournalEntryId { get; set; }
    public string? SettlementStatus { get; set; }
    public string? DiagnosticFlags { get; set; }
    public int DaysOutstanding { get; set; }
    public string AgingBucket { get; set; } = string.Empty;
}

public class CashRequirementForecastDto
{
    public DateTime AsOfDate { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public decimal TotalPayable { get; set; }
    public decimal OverdueAmount { get; set; }
    public List<CashRequirementPeriodDto> Periods { get; set; } = new();
}

public class CashRequirementPeriodDto
{
    public string Period { get; set; } = string.Empty; // "This Week", "Next Week", "Next 30 Days", etc.
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public decimal AmountDue { get; set; }
    public int InvoiceCount { get; set; }
    public decimal DiscountAvailable { get; set; }
}

public class SupplierStatementDto
{
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string? SupplierCode { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public decimal OpeningBalance { get; set; }
    public decimal TotalInvoices { get; set; }
    public decimal TotalPayments { get; set; }
    public decimal ClosingBalance { get; set; }
    public List<SupplierStatementLineDto> Lines { get; set; } = new();
}

public class SupplierStatementLineDto
{
    public DateTime Date { get; set; }
    public string TransactionType { get; set; } = string.Empty; // Invoice, Payment, CreditNote
    public string DocumentNumber { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public decimal Debit { get; set; }   // Invoices (increase payable)
    public decimal Credit { get; set; }  // Payments (decrease payable)
    public decimal RunningBalance { get; set; }
}

public class SupplierDetailedLedgerReportDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string CurrencyCode { get; set; } = "GHS";
    public bool ShowSupplierCurrency { get; set; }
    public decimal TotalOpeningBalance { get; set; }
    public decimal TotalDebits { get; set; }
    public decimal TotalCredits { get; set; }
    public decimal TotalClosingBalance { get; set; }
    public List<string> Warnings { get; set; } = new();
    public List<SupplierDetailedLedgerAccountDto> Suppliers { get; set; } = new();
}

public class SupplierDetailedLedgerAccountDto
{
    public Guid SupplierId { get; set; }
    public Guid? BusinessPartnerId { get; set; }
    public string SupplierCode { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = "GHS";
    public decimal OpeningBalance { get; set; }
    public decimal TotalDebits { get; set; }
    public decimal TotalCredits { get; set; }
    public decimal ClosingBalance { get; set; }
    public List<SupplierDetailedLedgerLineDto> Lines { get; set; } = new();
}

public class SupplierDetailedLedgerLineDto
{
    public Guid SourceDocumentId { get; set; }
    public DateTime TransactionDate { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public string DocumentNumber { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public string Description { get; set; } = string.Empty;
    public string TransactionCurrencyCode { get; set; } = "GHS";
    public decimal ExchangeRate { get; set; } = 1m;
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal RunningBalance { get; set; }
}

public class WithholdingTaxSummaryDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public decimal TotalWithheld { get; set; }
    public int SupplierCount { get; set; }
    public int TransactionCount { get; set; }
    public List<WithholdingTaxBySupplierDto> BySupplier { get; set; } = new();
}

public class WithholdingTaxBySupplierDto
{
    public Guid SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string? TaxId { get; set; }
    public decimal TotalInvoiceAmount { get; set; }
    public decimal TotalWithholdingTax { get; set; }
    public decimal TotalNetPayment { get; set; }
    public int TransactionCount { get; set; }
}

public class ApSummaryDto
{
    public decimal TotalOutstanding { get; set; }
    public decimal TotalOverdue { get; set; }
    public int OutstandingInvoiceCount { get; set; }
    public int OverdueInvoiceCount { get; set; }
    public decimal AverageDaysToPayment { get; set; }
    public decimal TotalPaidThisMonth { get; set; }
    public decimal DiscountsTaken { get; set; }
    public decimal DiscountsMissed { get; set; }
    public decimal WithholdingTaxThisMonth { get; set; }
    public int PendingApprovalCount { get; set; }
    public int PendingBatchCount { get; set; }
}

#endregion
