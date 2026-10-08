using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Sales;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// Represents a payment received from a customer (AR receipt)
/// A single payment can be allocated to multiple invoices
/// </summary>
public class CustomerPayment : BusinessEntity
{
    [Required]
    [MaxLength(50)]
    public string PaymentNumber { get; set; } = string.Empty;

    [Required]
    public Guid BusinessPartnerId { get; set; }
    public virtual BusinessPartner BusinessPartner { get; set; } = null!;

    [Required]
    public Guid BusinessPartnerRoleId { get; set; }
    public virtual BusinessPartnerRole BusinessPartnerRole { get; set; } = null!;

    [Required]
    public Guid BusinessPartnerArProfileVersionId { get; set; }
    public virtual BusinessPartnerArProfileVersion BusinessPartnerArProfileVersion { get; set; } = null!;

    [Required]
    [MaxLength(50)]
    public string BusinessPartnerCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string BusinessPartnerName { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? BusinessPartnerLegalName { get; set; }

    [MaxLength(100)]
    public string? BusinessPartnerTaxIdentificationNumber { get; set; }

    [Required]
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal AllocatedAmount { get; set; }

    [Column(TypeName = "decimal(20,6)")]
    public decimal RoundingAdjustmentAmount { get; set; }
    public Guid? FinanceRoundingEvidenceId { get; set; }

    /// <summary>
    /// True only when the original posted receipt was recorded to the configured customer-advance
    /// liability account. Later allocations must reclassify that advance through the Finance posting engine.
    /// </summary>
    public bool IsCustomerAdvance { get; set; }

    /// <summary>
    /// Classifies a receipt-shaped cutover fact whose cash movement occurred before go-live.
    /// Canonical CustomerPayment identity is retained for advance application and WHT inquiry,
    /// while the opening journal offsets migration clearing instead of a live bank account.
    /// </summary>
    [MaxLength(40)]
    public string? OpeningBalanceType { get; set; }
    public Guid? OpeningBalanceBatchId { get; set; }

    [MaxLength(100)]
    public string? OpeningSourceReference { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnallocatedAmount => TotalAmount - AllocatedAmount - RoundingAdjustmentAmount;

    [Required]
    [MaxLength(50)]
    public string PaymentMethod { get; set; } = "Cash"; // Cash, Check, BankTransfer, CreditCard, DebitCard, MobileMoney, Online
    public Guid? PaymentMethodId { get; set; }
    public virtual PaymentMethod? ConfiguredPaymentMethod { get; set; }

    [MaxLength(3)]
    public string CurrencyCode { get; set; } = "USD";

    [Column(TypeName = "decimal(18,6)")]
    public decimal ExchangeRate { get; set; } = 1.0m;

    /// <summary>
    /// Approved receipt-currency rate snapshot source. The value remains duplicated in
    /// ExchangeRate intentionally: the id supplies audit lineage while the value guarantees
    /// deterministic posting if rate-master data is later corrected.
    /// </summary>
    public Guid? ExchangeRateId { get; set; }

    // Bank/Payment Details
    public Guid? BankAccountId { get; set; }
    public virtual BankAccount? BankAccount { get; set; }

    /// <summary>
    /// Holding/settlement account used for cash, cheque, mobile-money, and card receipts.
    /// Direct bank transfers use BankAccountId instead.
    /// </summary>
    public Guid? LiquidityAccountId { get; set; }
    public virtual LiquidityAccount? LiquidityAccount { get; set; }

    public Guid? LiquidityAccountEntryId { get; set; }
    public virtual LiquidityAccountEntry? LiquidityAccountEntry { get; set; }

    [MaxLength(100)]
    public string? CheckNumber { get; set; }

    [MaxLength(150)]
    public string? ChequeDrawerBank { get; set; }

    [MaxLength(100)]
    public string? TransactionReference { get; set; }

    public Guid? WithholdingTaxId { get; set; }
    public virtual Tax? WithholdingTax { get; set; }

    public Guid? WithholdingTaxAccountId { get; set; }
    public virtual Account? WithholdingTaxAccount { get; set; }

    /// <summary>
    /// Functional/statutory WHT roll-up derived from the active invoice allocations. The native
    /// invoice-currency evidence is retained on PaymentAllocation for posting and audit trace.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal WithholdingTaxAmount { get; set; }

    public Guid? VatWithholdingTaxId { get; set; }
    public virtual Tax? VatWithholdingTax { get; set; }

    public Guid? VatWithholdingAccountId { get; set; }
    public virtual Account? VatWithholdingAccount { get; set; }

    /// <summary>
    /// Functional/statutory VAT-WHT roll-up derived from the active invoice allocations.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal VatWithholdingAmount { get; set; }

    [MaxLength(100)]
    public string? WithholdingCertificateNumber { get; set; }

    public DateTime? WithholdingCertificateDate { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = "Pending"; // Pending, Cleared, Bounced, Cancelled

    public DateTime? ClearedDate { get; set; }

    // Credit Note handling
    public bool IsCreditNote { get; set; } = false;
    public Guid? CreditNoteId { get; set; }

    // GL Posting
    public Guid? JournalEntryId { get; set; }
    public Guid? SourceBookAuthorityId { get; set; }
    public virtual FinanceSourceBookAuthority? SourceBookAuthority { get; set; }

    /// <summary>
    /// Durable lineage for a controlled posted-receipt reversal. The original receipt, journal,
    /// cash/liquidity entry, and allocations remain in history; these links identify the immutable
    /// compensating records created to remove their financial and operational effect.
    /// </summary>
    public Guid? ReversalJournalEntryId { get; set; }
    public Guid? ReversalPostingEventId { get; set; }
    public Guid? ReversalCashTransactionId { get; set; }
    public Guid? ReversalLiquidityAccountEntryId { get; set; }
    public DateTime? ReversalDate { get; set; }
    public DateTime? ReversedAt { get; set; }
    public Guid? ReversedById { get; set; }

    [MaxLength(1000)]
    public string? ReversalReason { get; set; }

    // Multi-tenant


    // Navigation properties
    public virtual ICollection<PaymentAllocation> Allocations { get; set; } = new List<PaymentAllocation>();
    public virtual SalesOrderCustomerDeposit? SalesOrderDeposit { get; set; }
}
