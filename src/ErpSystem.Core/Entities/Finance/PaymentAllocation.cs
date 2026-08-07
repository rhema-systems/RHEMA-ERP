using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// Represents the allocation of a customer payment to a specific invoice
/// Supports partial and multi-invoice payment allocation
/// </summary>
public class PaymentAllocation : BaseEntity
{
    [Required]
    public Guid CustomerPaymentId { get; set; }
    public virtual CustomerPayment CustomerPayment { get; set; } = null!;

    [Required]
    public Guid InvoiceId { get; set; }
    public virtual Invoice Invoice { get; set; } = null!;

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal AllocatedAmount { get; set; }

    /// <summary>
    /// Portion of the receipt consumed by this allocation in receipt currency. AllocatedAmount
    /// remains the invoice reduction in invoice currency; they only match for same-currency AR.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal PaymentCurrencyAmount { get; set; }

    [Required]
    [MaxLength(3)]
    public string InvoiceCurrencyCode { get; set; } = "GHS";

    [Required]
    [MaxLength(3)]
    public string PaymentCurrencyCode { get; set; } = "GHS";

    public bool IsCrossCurrency { get; set; }

    /// <summary>
    /// Immutable approved-rate evidence and calculated functional values used by posting,
    /// reversal, settlement read-model rebuilds, and audit traces.
    /// </summary>
    public Guid? InvoiceSettlementExchangeRateId { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal InvoiceSettlementExchangeRate { get; set; } = 1m;

    public Guid? PaymentExchangeRateId { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal PaymentExchangeRate { get; set; } = 1m;

    [Column(TypeName = "decimal(18,2)")]
    public decimal PaymentFunctionalAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal SettlementFunctionalAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountAmount { get; set; } = 0;

    /// <summary>
    /// Frozen functional-currency value posted for the invoice-currency discount. This is kept
    /// separately because the receipt cash and invoice deduction may use different currencies.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountFunctionalAmount { get; set; }

    /// <summary>
    /// WHT and VAT-WHT are stored per invoice in invoice currency. Their functional snapshots
    /// are the statutory/reporting values and allow a single receipt to settle invoices in
    /// different currencies without inventing a header currency for the deductions.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal WithholdingTaxAmount { get; set; }

    /// <summary>
    /// Functional/statutory value of WHT suffered on this invoice allocation.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal WithholdingTaxFunctionalAmount { get; set; }

    /// <summary>
    /// VAT withholding suffered in the invoice currency.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal VatWithholdingAmount { get; set; }

    /// <summary>
    /// Frozen functional/statutory value of the allocation's VAT withholding component.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal VatWithholdingFunctionalAmount { get; set; }

    public DateTime AllocationDate { get; set; } = DateTime.UtcNow;

    [MaxLength(500)]
    public string? Notes { get; set; }

    // Track if this is a reversal/adjustment
    public bool IsReversal { get; set; } = false;
    public Guid? OriginalAllocationId { get; set; }

    /// <summary>
    /// Present only when a previously posted customer advance is applied. This records the
    /// engine-created advance-to-AR-control reclassification without altering the cash receipt.
    /// </summary>
    public Guid? ApplicationJournalEntryId { get; set; }
    public Guid? ApplicationPostingEventId { get; set; }

    // Multi-tenant
    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;
}
