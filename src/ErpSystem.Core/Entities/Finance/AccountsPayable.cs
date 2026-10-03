using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Procurement;
using ErpSystem.Core.Entities.Finance.FixedAssets;
using ErpSystem.Core.Entities.Inventory;
using ErpSystem.Core.Entities.Workflow;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Finance;

#region Enums

/// <summary>Server-owned land acquisition payable source.</summary>
public enum EstatePayableKind { SurveyorFee = 1, VendorConsideration = 2, StampDuty = 3, OtherAcquisitionCosts = 4 }

/// <summary>Server-owned component of an IFRS 16 lease instalment AP draft.</summary>
public enum LeaseInvoiceComponent { Principal = 1, Interest = 2 }

/// <summary>
/// Status of a vendor/supplier invoice through its lifecycle.
/// </summary>
public enum VendorInvoiceStatus
{
    Draft = 1,
    PendingApproval = 2,
    Approved = 3,
    PartiallyPaid = 4,
    Paid = 5,
    Overdue = 6,
    Voided = 7,
    Rejected = 8,
    OnHold = 9
}

/// <summary>
/// Matching type selected for a vendor invoice.
/// </summary>
public enum InvoiceMatchingType
{
    None = 0,
    TwoWay = 1,   // Invoice vs Purchase Order
    ThreeWay = 2  // Invoice vs PO vs Goods Receipt
}

/// <summary>
/// Result of the matching verification process.
/// </summary>
public enum InvoiceMatchingStatus
{
    Unmatched = 0,
    TwoWayMatched = 1,
    ThreeWayMatched = 2,
    MatchException = 3
}

/// <summary>
/// Status of a vendor payment.
/// </summary>
public enum VendorPaymentStatus
{
    Draft = 1,
    PendingAuthorization = 2,
    Authorized = 3,
    Processed = 4,
    Cleared = 5,
    Voided = 6,
    Failed = 7,
    Reconciled = 8,

    /// <summary>
    /// The payment remains in history, but a linked compensating journal and allocation records
    /// have fully reversed its accounting and subledger effect.
    /// </summary>
    Reversed = 9
}

/// <summary>
/// Method used to pay a vendor.
/// </summary>
public enum VendorPaymentMethod
{
    BankTransfer = 1,
    Cheque = 2,
    Cash = 3,
    WireTransfer = 4,
    MobileMoney = 5,
    DirectDebit = 6,
    Other = 99
}

/// <summary>
/// Status of a payment batch.
/// </summary>
public enum PaymentBatchStatus
{
    Draft = 1,
    PendingApproval = 2,
    Approved = 3,
    Processing = 4,
    Completed = 5,
    PartiallyCompleted = 6,
    Cancelled = 7
}

#endregion

#region Vendor Invoice

/// <summary>
/// Represents a supplier/vendor invoice in the Accounts Payable module.
/// The canonical counterparty is a Procurement Business Partner. Captured identity fields are
/// immutable accounting evidence and are not re-derived from the mutable partner master.
/// </summary>
public class VendorInvoice : TenantEntity
{
    /// <summary>Server-owned IFRS 16 schedule source. Generic AP clients cannot assign it.</summary>
    public Guid? LeaseScheduleLineId { get; set; }
    public virtual LeaseScheduleLine? LeaseScheduleLine { get; set; }
    /// <summary>Retained voided lease invoice replaced by this governed successor.</summary>
    public Guid? ReplacesLeaseVendorInvoiceId { get; set; }
    public virtual VendorInvoice? ReplacesLeaseVendorInvoice { get; set; }
    public virtual ICollection<VendorInvoice> LeaseReplacementInvoices { get; set; } = new List<VendorInvoice>();
    public Guid? LeaseAccountingBookId { get; set; }
    [MaxLength(20)] public string? LeaseAccountingBookCode { get; set; }
    [MaxLength(3)] public string? LeaseFunctionalCurrencyCode { get; set; }
    /// <summary>Server-owned immutable book authority for governed posting and settlement.</summary>
    public Guid? SourceBookAuthorityId { get; set; }
    public FinanceSourceBookAuthority? SourceBookAuthority { get; set; }
    public Guid? EstateAcquisitionId { get; set; }
    public EstatePayableKind? EstatePayableKind { get; set; }
    /// <summary>Reviewed Procurement distribution overrides; applied by the shared posting builder.</summary>
    public string? DistributionDraftJson { get; set; }
    /// <summary>Server-owned receipt consolidation request identity; manual AP remains null.</summary>
    public Guid? AutoInvoiceRequestId { get; set; }
    [MaxLength(64)] public string? AutoInvoiceRequestHash { get; set; }
    // ── Identification ──────────────────────────────────────────────────

    [Required]
    [MaxLength(50)]
    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>
    /// The supplier's own invoice/reference number printed on the physical invoice.
    /// </summary>
    [MaxLength(100)]
    public string? SupplierInvoiceNumber { get; set; }

    // ── Supplier ────────────────────────────────────────────────────────

    [Required]
    public Guid BusinessPartnerId { get; set; }
    public virtual BusinessPartner BusinessPartner { get; set; } = null!;

    public Guid? BusinessPartnerRoleId { get; set; }
    public virtual BusinessPartnerRole? BusinessPartnerRole { get; set; }

    public Guid? BusinessPartnerApProfileVersionId { get; set; }
    public virtual BusinessPartnerApProfileVersion? BusinessPartnerApProfileVersion { get; set; }

    [Required]
    [MaxLength(200)]
    public string SupplierName { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string BusinessPartnerCode { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? BusinessPartnerLegalName { get; set; }

    [MaxLength(100)]
    public string? BusinessPartnerTaxIdentificationNumber { get; set; }

    // ── Purchase Order Link (for matching) ──────────────────────────────

    public Guid? PurchaseOrderId { get; set; }
    public virtual PurchaseOrder? PurchaseOrder { get; set; }

    // ── Dates ───────────────────────────────────────────────────────────

    [Required]
    public DateTime InvoiceDate { get; set; }

    public DateTime? ReceivedDate { get; set; }

    public DateTime? DueDate { get; set; }

    // ── Financial ───────────────────────────────────────────────────────

    [Column(TypeName = "decimal(20,4)")]
    public decimal SubTotal { get; set; }

    [Column(TypeName = "decimal(20,4)")]
    public decimal TaxAmount { get; set; }

    [Column(TypeName = "decimal(20,4)")]
    public decimal DiscountAmount { get; set; }

    [Column(TypeName = "decimal(20,4)")]
    public decimal TotalAmount { get; set; }

    [Column(TypeName = "decimal(20,4)")]
    public decimal PaidAmount { get; set; }

    [NotMapped]
    public decimal BalanceAmount => TotalAmount - PaidAmount;

    // ── Currency ────────────────────────────────────────────────────────

    [Required]
    [MaxLength(3)]
    public string CurrencyCode { get; set; } = "USD";

    /// <summary>
    /// Required for a manually captured invoice whose transaction currency differs from the
    /// supplier master currency. PO-backed invoices inherit their governed source currency.
    /// </summary>
    [MaxLength(500)]
    public string? CurrencyOverrideReason { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal ExchangeRate { get; set; } = 1.0m;

    /// <summary>
    /// Approved tenant exchange-rate record frozen for a governed foreign-currency opening
    /// invoice. Ordinary legacy invoices may remain null until their FX entry contract is
    /// migrated independently.
    /// </summary>
    public Guid? ExchangeRateId { get; set; }
    public virtual ExchangeRate? ExchangeRateRecord { get; set; }

    [Column(TypeName = "decimal(20,4)")]
    public decimal BaseCurrencyAmount { get; set; }

    // ── Payment Terms ───────────────────────────────────────────────────

    public int PaymentTermsDays { get; set; } = 30;

    public Guid? PaymentTermId { get; set; }
    public virtual PaymentTerm? PaymentTerm { get; set; }

    // ── Early-Payment Discount ──────────────────────────────────────────

    [Column(TypeName = "decimal(5,2)")]
    public decimal EarlyPaymentDiscountPercentage { get; set; }

    public DateTime? EarlyPaymentDiscountDueDate { get; set; }

    [Column(TypeName = "decimal(20,4)")]
    public decimal EarlyPaymentDiscountAmount { get; set; }

    // ── Withholding Tax ─────────────────────────────────────────────────

    [Column(TypeName = "decimal(18,4)")]
    public decimal WithholdingTaxRate { get; set; }
    public bool? ApplySupplierWithholdingDefaults { get; set; }
    [Column(TypeName = "decimal(18,4)")]
    public decimal? WithholdingTaxRateOverride { get; set; }
    public bool WithholdingDecisionPending { get; set; }

    [Column(TypeName = "decimal(20,4)")]
    public decimal WithholdingTaxAmount { get; set; }

    public Guid? WithholdingTaxId { get; set; }
    public virtual Tax? WithholdingTax { get; set; }

    public Guid? WithholdingTaxAccountId { get; set; }
    public virtual Account? WithholdingTaxAccount { get; set; }

    [MaxLength(100)]
    public string? WithholdingCertificateNumber { get; set; }

    public DateTime? WithholdingCertificateDate { get; set; }

    /// <summary>
    /// Stable contract/reference used with the supply category to scope statutory WHT
    /// threshold accumulation. Required whenever a WHT tax is selected.
    /// </summary>
    [MaxLength(100)]
    public string? WithholdingContractReference { get; set; }

    public WhtSupplyCategory? WithholdingSupplyCategory { get; set; }

    // ── Matching ────────────────────────────────────────────────────────

    public InvoiceMatchingType MatchingType { get; set; } = InvoiceMatchingType.None;

    public InvoiceMatchingStatus MatchingStatus { get; set; } = InvoiceMatchingStatus.Unmatched;

    [MaxLength(2000)]
    public string? MatchingNotes { get; set; }

    /// <summary>
    /// Immutable TDC-0504 control-event lineage for the latest server-derived
    /// three-way evaluation. PO-linked invoices cannot enter approval without it.
    /// </summary>
    public Guid? MatchingControlEventId { get; set; }

    [MaxLength(64)]
    public string? MatchingSnapshotHash { get; set; }

    public DateTime? MatchingEvaluatedAtUtc { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal MatchingPriceTolerancePercent { get; set; }

    [Column(TypeName = "decimal(5,2)")]
    public decimal MatchingQuantityTolerancePercent { get; set; }

    /// <summary>
    /// Optional independently approved AP-006 exception event. TDC-0504 only
    /// consumes this trusted outcome; TDC-0507 owns its request/approval/report lifecycle.
    /// </summary>
    public Guid? MatchExceptionControlEventId { get; set; }

    // ── Authoritative supply acceptance lineage ────────────────────────

    /// <summary>
    /// Typed owner of the accepted performance record used by matching.
    /// Goods remains owned by Procurement receiving/inspection, Services by
    /// Projects deliverables, and Works by QS payment certificates.
    /// </summary>
    public ProcurementAcceptedSupplyKind? AcceptedSupplyKind { get; set; }

    public Guid? AcceptedSupplySourceId { get; set; }

    [MaxLength(100)]
    public string? AcceptedSupplySourceReference { get; set; }

    [MaxLength(64)]
    public string? AcceptedSupplySnapshotHash { get; set; }

    public DateTime? AcceptedSupplyValidatedAtUtc { get; set; }

    // ── Status & Approval ───────────────────────────────────────────────

    public VendorInvoiceStatus Status { get; set; } = VendorInvoiceStatus.Draft;

    [MaxLength(50)]
    public string ApprovalStatus { get; set; } = "Draft"; // Draft, PendingApproval, Approved, Rejected, NotRequired
    public bool ApprovalRequired { get; set; } = true;

    public Guid? SubmittedById { get; set; }
    public DateTime? SubmittedDate { get; set; }

    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedDate { get; set; }

    [MaxLength(1000)]
    public string? ApprovalComments { get; set; }

    // ── GL Posting ──────────────────────────────────────────────────────

    public Guid? ExpenseAccountId { get; set; }
    public virtual Account? ExpenseAccount { get; set; }

    public Guid? ApAccountId { get; set; }
    public virtual Account? ApAccount { get; set; }

    /// <summary>
    /// Supplier input-tax fallback captured when a new invoice opts into partner defaults.
    /// Tax-rule accounts take precedence; nonrecoverable tax never uses this account.
    /// </summary>
    public Guid? SupplierTaxFallbackAccountId { get; set; }
    public virtual Account? SupplierTaxFallbackAccount { get; set; }

    public Guid? JournalEntryId { get; set; }

    // ── Notes & Reference ───────────────────────────────────────────────

    [MaxLength(500)]
    public string? Notes { get; set; }

    [MaxLength(100)]
    public string? Reference { get; set; }

    // ── Multi-tenant ────────────────────────────────────────────────────

    public bool IsOpeningBalance { get; set; }

    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;

    // ── Navigation ──────────────────────────────────────────────────────

    public virtual ICollection<VendorInvoiceLineItem> LineItems { get; set; } = new List<VendorInvoiceLineItem>();
    public virtual ICollection<VendorPaymentAllocation> PaymentAllocations { get; set; } = new List<VendorPaymentAllocation>();

    /// <summary>
    /// Finance-owned supplier debit-note applications that reduce this invoice's AP balance.
    /// They are separate from cash allocations because the debit note has already posted its
    /// own AP-control reduction and must not be posted again as part of the payment journal.
    /// </summary>
    public virtual ICollection<SupplierDebitNoteApplication> SupplierDebitNoteApplications { get; set; }
        = new List<SupplierDebitNoteApplication>();
}

/// <summary>
/// An individual line item on a vendor invoice.
/// </summary>
public class VendorInvoiceLineItem : TenantEntity
{
    /// <summary>Server-owned immutable lease component; null for ordinary AP lines.</summary>
    public LeaseInvoiceComponent? LeaseComponent { get; set; }
    /// <summary>Posted landed-cost charge cleared by this AP line; assigned only by the AP handoff.</summary>
    public Guid? LandedCostItemId { get; set; }

    [Required]
    public Guid VendorInvoiceId { get; set; }
    public virtual VendorInvoice VendorInvoice { get; set; } = null!;

    // ── Line Item Type ──────────────────────────────────────────────────

    /// <summary>
    /// Whether this line item maps to an inventory product or a GL expense account.
    /// </summary>
    [MaxLength(20)]
    public string LineItemType { get; set; } = "Expense"; // Expense, Product

    // ── For GL-account-based lines ──────────────────────────────────────

    public Guid? GLAccountId { get; set; }
    public virtual Account? GLAccount { get; set; }

    /// <summary>
    /// Canonical adopted Finance budget cell selected for this direct expense line. The
    /// relationship is optional because opening, inventory, fixed-asset and Procurement/GRV
    /// lines do not create a second AP budget commitment. When the resolved expense account is
    /// budget-controlled, submission requires this evidence and Finance derives both the
    /// reservation and the posted dimension set from it.
    /// </summary>
    public Guid? BudgetEntryId { get; set; }
    public virtual BudgetEntry? BudgetEntry { get; set; }

    public Guid? FixedAssetId { get; set; }
    public virtual FixedAsset? FixedAsset { get; set; }

    public Guid? CapitalizationJournalEntryId { get; set; }
    public Guid? CapitalizationPostingEventId { get; set; }
    public DateTime? CapitalizedAt { get; set; }

    /// <summary>
    /// When the containing AP invoice is voided, these fields prove that its shared reversal
    /// journal also removed this line's asset-register cost. A separate asset journal is never
    /// posted because doing so would reverse the invoice's AP/tax lines twice.
    /// </summary>
    public Guid? CapitalizationReversalJournalEntryId { get; set; }
    public Guid? CapitalizationReversalPostingEventId { get; set; }
    public DateTime? CapitalizationReversedAt { get; set; }

    // ── For product-based lines (links to PO item for matching) ─────────

    /// <summary>
    /// Legacy procurement PO line link. Finance PO/GRV conversions must not store
    /// FinancePurchaseOrderItem ids here because this FK targets PurchaseOrderItems.
    /// </summary>
    public Guid? PurchaseOrderItemId { get; set; }
    public virtual PurchaseOrderItem? PurchaseOrderItem { get; set; }

    public Guid? InventoryItemId { get; set; }
    public virtual InventoryItem? InventoryItem { get; set; }

    // ── Inventory destination tracking ──────────────────────────────────
    public Guid? WarehouseId { get; set; }
    public virtual Warehouse? Warehouse { get; set; }
    
    public Guid? LocationId { get; set; }
    public virtual WarehouseLocation? Location { get; set; }

    // ── Inventory item tracking details ─────────────────────────────────
    [MaxLength(100)]
    public string? SerialNumber { get; set; }
    
    [MaxLength(100)]
    public string? LotNumber { get; set; }
    
    public DateTime? ExpirationDate { get; set; }

    // ── Description & Amounts ───────────────────────────────────────────

    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,4)")]
    public decimal Quantity { get; set; } = 1;

    [Required]
    [Column(TypeName = "decimal(20,6)")]
    public decimal UnitPrice { get; set; }

    [NotMapped]
    public decimal LineTotal => Quantity * UnitPrice;

    // ── Tax ──────────────────────────────────────────────────────────────

    public Guid? TaxGroupId { get; set; }
    public virtual TaxGroup? TaxGroup { get; set; }

    public TaxTreatment TaxTreatment { get; set; } = TaxTreatment.Standard;

    [Column(TypeName = "decimal(18,6)")]
    public decimal TaxRate { get; set; }

    [Column(TypeName = "decimal(20,4)")]
    public decimal TaxAmount { get; set; }

    [MaxLength(50)]
    public string? TaxCode { get; set; }

    // ── Discount ────────────────────────────────────────────────────────

    [Column(TypeName = "decimal(5,2)")]
    public decimal DiscountPercentage { get; set; }

    [Column(TypeName = "decimal(20,4)")]
    public decimal DiscountAmount { get; set; }

    // ── Unit of Measure ─────────────────────────────────────────────────

    [MaxLength(50)]
    public string? Unit { get; set; }

    // ── Multi-tenant ────────────────────────────────────────────────────

    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;
}

#endregion

#region Vendor Payment

/// <summary>
/// Represents a payment made to a supplier/vendor.
/// A single payment can be allocated across multiple invoices.
/// </summary>
public class VendorPayment : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string PaymentNumber { get; set; } = string.Empty;

    // ── Canonical Business Partner ─────────────────────────────────────

    [Required]
    public Guid BusinessPartnerId { get; set; }
    public virtual BusinessPartner BusinessPartner { get; set; } = null!;

    /// <summary>
    /// Role/profile lineage is captured at payment creation. Settlement may continue after the
    /// role is inactivated, but new advances must still resolve an approved effective AP profile.
    /// </summary>
    public Guid? BusinessPartnerRoleId { get; set; }
    public virtual BusinessPartnerRole? BusinessPartnerRole { get; set; }
    public Guid? BusinessPartnerApProfileVersionId { get; set; }
    public virtual BusinessPartnerApProfileVersion? BusinessPartnerApProfileVersion { get; set; }

    [Required, MaxLength(50)]
    public string BusinessPartnerCode { get; set; } = string.Empty;
    [Required, MaxLength(200)]
    public string BusinessPartnerName { get; set; } = string.Empty;
    [MaxLength(200)]
    public string? BusinessPartnerLegalName { get; set; }
    [MaxLength(100)]
    public string? BusinessPartnerTaxIdentificationNumber { get; set; }

    // ── Financial ───────────────────────────────────────────────────────

    [Required]
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal AllocatedAmount { get; set; }

    /// <summary>
    /// True only when the original posted payment was recorded to the configured supplier-advance
    /// account. Later allocations must reclassify that advance through the Finance posting engine.
    /// </summary>
    public bool IsSupplierAdvance { get; set; }

    /// <summary>
    /// Identifies a canonical AP record created from approved cutover evidence rather than a
    /// current-period bank disbursement. The type distinguishes supplier advances from WHT
    /// liabilities so downstream settlement and compliance services can reuse this payment
    /// without mistaking the migration-clearing journal for a cash movement.
    /// </summary>
    [MaxLength(40)]
    public string? OpeningBalanceType { get; set; }
    public Guid? OpeningBalanceBatchId { get; set; }

    [MaxLength(100)]
    public string? OpeningSourceReference { get; set; }

    [NotMapped]
    public decimal UnallocatedAmount => TotalAmount - AllocatedAmount;

    // ── Payment Method ──────────────────────────────────────────────────

    public VendorPaymentMethod PaymentMethod { get; set; } = VendorPaymentMethod.BankTransfer;
    public Guid? PaymentMethodId { get; set; }
    public virtual PaymentMethod? ConfiguredPaymentMethod { get; set; }

    // ── Currency ────────────────────────────────────────────────────────

    [MaxLength(3)]
    public string CurrencyCode { get; set; } = "USD";

    [Column(TypeName = "decimal(18,6)")]
    public decimal ExchangeRate { get; set; } = 1.0m;

    /// <summary>
    /// Approved payment-currency rate selected for this payment. Cross-currency settlement uses
    /// this stable reference instead of trusting a later lookup or an untraceable typed value.
    /// </summary>
    public Guid? ExchangeRateId { get; set; }

    /// <summary>
    /// Exact accounting-book authority inherited from the posted invoices settled by this
    /// payment. It is frozen before the first payment journal and reused by FX and reversals.
    /// </summary>
    public Guid? AccountingBookId { get; set; }
    [MaxLength(20)] public string? AccountingBookCode { get; set; }
    [MaxLength(3)] public string? FunctionalCurrencyCode { get; set; }

    // ── Bank Details ────────────────────────────────────────────────────

    public Guid? BankAccountId { get; set; }
    public virtual BankAccount? BankAccount { get; set; }

    [MaxLength(100)]
    public string? ChequeNumber { get; set; }

    [MaxLength(100)]
    public string? TransactionReference { get; set; }

    // ── Withholding Tax ─────────────────────────────────────────────────

    [Column(TypeName = "decimal(5,2)")]
    public decimal WithholdingTaxRate { get; set; }

    /// <summary>
    /// Functional/statutory WHT roll-up derived from the active invoice allocations. It must not
    /// be interpreted as payment-currency cash when a payment settles foreign-currency invoices.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal WithholdingTaxAmount { get; set; }

    /// <summary>
    /// Gross settlement base evaluated by the configured WHT policy. This is persisted rather
    /// than reconstructed from net cash so threshold and certificate evidence remain exact.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal WithholdingTaxBaseAmount { get; set; }

    /// <summary>
    /// Cumulative eligible supplier payments before this transaction. Together with the
    /// configured threshold snapshot it explains why WHT did or did not apply at entry time.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal WithholdingTaxCumulativeBefore { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? WithholdingTaxThresholdAmount { get; set; }

    public bool WithholdingTaxThresholdApplied { get; set; }

    [MaxLength(500)]
    public string? WithholdingTaxCalculationNote { get; set; }

    public Guid? WithholdingTaxId { get; set; }
    public virtual Tax? WithholdingTax { get; set; }

    public Guid? WithholdingTaxAccountId { get; set; }
    public virtual Account? WithholdingTaxAccount { get; set; }

    [MaxLength(100)]
    public string? WithholdingCertificateNumber { get; set; }

    public DateTime? WithholdingCertificateDate { get; set; }

    // ── Early-Payment Discount Applied ──────────────────────────────────

    /// <summary>
    /// Functional-currency roll-up of allocation discounts. Native discount evidence remains on
    /// each allocation because one payment may settle invoices in different currencies.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountTaken { get; set; }

    // ── Status & Authorization ──────────────────────────────────────────

    public VendorPaymentStatus Status { get; set; } = VendorPaymentStatus.Draft;

    /// <summary>Server-captured approval requirement; existing payments retain their approval route.</summary>
    public bool ApprovalRequired { get; set; } = true;

    /// <summary>
    /// Direct payments use the platform workflow just like payment batches. These fields retain
    /// the submission and selected authority route on the canonical payment instead of requiring
    /// auditors to infer them from mutable workflow configuration.
    /// </summary>
    public Guid? SubmittedById { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public Guid? AppliedApprovalPolicySetId { get; set; }

    [MaxLength(100)]
    public string? AppliedApprovalPolicyCode { get; set; }

    /// <summary>
    /// Immutable JSON snapshot and SHA-256 digest of the effective approval/evidence policy used
    /// at submission. Published policies are immutable, but the snapshot also protects an in-flight
    /// payment if an administrator later retires the source policy.
    /// </summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? ApprovalControlSnapshotJson { get; set; }

    [MaxLength(64)]
    public string? ApprovalControlSnapshotHash { get; set; }

    /// <summary>
    /// Exceptional payments are deliberately declared by the maker and always enter the Managing
    /// Director authority route. This is separate from automatic high-value routing so both the
    /// policy reason and the operator's exceptional-business reason remain visible.
    /// </summary>
    public bool IsExceptionalPayment { get; set; }

    [MaxLength(1000)]
    public string? ExceptionalPaymentReason { get; set; }

    public bool RequiresManagingDirectorApproval { get; set; }
    public Guid? ManagingDirectorApprovedById { get; set; }
    public DateTime? ManagingDirectorApprovedAt { get; set; }

    /// <summary>
    /// A requested evidence exception is not an automatic waiver. It is finalized only when the
    /// workflow completes through the configured Managing Director approval group.
    /// </summary>
    public bool EvidenceExceptionRequested { get; set; }

    [MaxLength(1000)]
    public string? EvidenceExceptionReason { get; set; }

    public Guid? EvidenceExceptionRequestedById { get; set; }
    public DateTime? EvidenceExceptionRequestedAt { get; set; }
    public Guid? EvidenceExceptionApprovedById { get; set; }
    public DateTime? EvidenceExceptionApprovedAt { get; set; }

    public Guid? AuthorizedById { get; set; }
    public DateTime? AuthorizedDate { get; set; }

    /// <summary>
    /// Immutable AP-004/TDC-0506 decision proving that the payment approver
    /// was independent of every allocated invoice processor.
    /// </summary>
    public Guid? InvoicePaymentSodControlEventId { get; set; }
    public virtual ProcurementControlEvent? InvoicePaymentSodControlEvent { get; set; }

    public DateTime? ClearedDate { get; set; }

    // ── Batch Link ──────────────────────────────────────────────────────

    public Guid? PaymentBatchId { get; set; }
    public virtual PaymentBatch? PaymentBatch { get; set; }

    // ── GL Posting ──────────────────────────────────────────────────────

    public Guid? JournalEntryId { get; set; }

    /// <summary>
    /// Explicit links to the compensating posting created for a posted-payment reversal. These are
    /// stored on the payment so operational screens do not have to infer reversal state from
    /// journal flags alone. The original payment and posting remain immutable audit evidence.
    /// </summary>
    public Guid? ReversalJournalEntryId { get; set; }
    public Guid? ReversalPostingEventId { get; set; }
    public DateTime? ReversalDate { get; set; }
    public DateTime? ReversedAt { get; set; }
    public Guid? ReversedById { get; set; }

    [MaxLength(1000)]
    public string? ReversalReason { get; set; }

    // ── Notes ───────────────────────────────────────────────────────────

    [MaxLength(500)]
    public string? Notes { get; set; }

    // ── Multi-tenant ────────────────────────────────────────────────────

    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;

    // ── Navigation ──────────────────────────────────────────────────────

    public virtual ICollection<VendorPaymentAllocation> Allocations { get; set; } = new List<VendorPaymentAllocation>();

    /// <summary>
    /// Finance AP settlement bridge for supplier credits consumed alongside this payment.
    /// These rows do not consume payment cash and therefore do not change AllocatedAmount.
    /// </summary>
    public virtual ICollection<SupplierDebitNoteApplication> SupplierDebitNoteApplications { get; set; }
        = new List<SupplierDebitNoteApplication>();
}

/// <summary>
/// Represents the allocation of a vendor payment to a specific vendor invoice.
/// Supports partial and multi-invoice payment allocation.
/// </summary>
public class VendorPaymentAllocation : TenantEntity
{
    [Required]
    public Guid VendorPaymentId { get; set; }
    public virtual VendorPayment VendorPayment { get; set; } = null!;

    [Required]
    public Guid VendorInvoiceId { get; set; }
    public virtual VendorInvoice VendorInvoice { get; set; } = null!;

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal AllocatedAmount { get; set; }

    /// <summary>
    /// Cash or supplier-advance lot consumed in payment currency. AllocatedAmount is deliberately
    /// retained as the invoice-currency reduction for aging and invoice balance; when this row is
    /// a posted advance application, the two amounts may differ and are reversed as one pair.
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
    /// Approved rate ids and frozen values retain both audit lineage and deterministic arithmetic
    /// if the exchange-rate master is corrected after posting. For an advance application the
    /// payment rate is the advance's origin rate and the invoice rate is the application-date rate.
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
    public decimal DiscountAmount { get; set; }

    /// <summary>
    /// Functional-currency value of the invoice-currency discount. Keeping this beside the
    /// native amount prevents later rate-master edits from changing the posted deduction.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal DiscountFunctionalAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal WithholdingTaxAmount { get; set; }

    /// <summary>
    /// Functional/statutory value of this invoice's WHT component. The header remains a roll-up;
    /// allocation evidence is authoritative when invoices or payment currency differ.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal WithholdingTaxFunctionalAmount { get; set; }

    /// <summary>Frozen net supply component in functional currency; null denotes pre-v2 evidence.</summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? WithholdingTaxBaseFunctionalAmount { get; set; }

    /// <summary>
    /// Approved Bank of Ghana statutory rate used to convert this allocation's invoice-currency
    /// WHT basis into GHS. Null for a native GHS allocation.
    /// </summary>
    public Guid? WithholdingTaxStatutoryExchangeRateId { get; set; }

    [Column(TypeName = "decimal(18,6)")]
    public decimal? WithholdingTaxStatutoryExchangeRate { get; set; }

    public DateTime? WithholdingTaxStatutoryExchangeRateDate { get; set; }

    [MaxLength(100)]
    public string? WithholdingTaxStatutoryExchangeRateSource { get; set; }

    [MaxLength(1000)]
    public string? WithholdingTaxStatutoryExchangeRateReference { get; set; }

    public DateTime AllocationDate { get; set; } = DateTime.UtcNow;

    [MaxLength(500)]
    public string? Notes { get; set; }

    // Track reversals
    public bool IsReversal { get; set; } = false;
    public Guid? OriginalAllocationId { get; set; }

    /// <summary>
    /// Present only when a previously posted supplier advance is applied. The allocation's
    /// reclassification journal/event are immutable evidence; ordinary pre-post allocations
    /// do not create a second journal.
    /// </summary>
    public Guid? ApplicationJournalEntryId { get; set; }
    public Guid? ApplicationPostingEventId { get; set; }

    /// <summary>
    /// Immutable AP-003 decision lineage captured immediately before the
    /// positive settlement mutation. Reversals deliberately do not require a
    /// new readiness decision because they release rather than consume funds.
    /// </summary>
    public Guid? PaymentReadinessControlEventId { get; set; }
    public virtual ProcurementControlEvent? PaymentReadinessControlEvent { get; set; }

    [MaxLength(64)]
    public string? PaymentReadinessSnapshotHash { get; set; }

    public DateTime? PaymentReadinessEvaluatedAtUtc { get; set; }

    // ── Multi-tenant ────────────────────────────────────────────────────

    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;
}

#endregion

#region Payment Batch

/// <summary>
/// Groups vendor payments for bulk processing and authorization.
/// Payments within a batch share the same authorization workflow and processing date.
/// </summary>
public class PaymentBatch : TenantEntity
{
    [Required]
    [MaxLength(50)]
    public string BatchNumber { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Description { get; set; }

    // ── Date Range (invoices due in this window) ────────────────────────

    public DateTime BatchDate { get; set; } = DateTime.UtcNow;

    public DateTime? DueDateFrom { get; set; }
    public DateTime? DueDateTo { get; set; }

    // ── Totals ──────────────────────────────────────────────────────────

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    public int PaymentCount { get; set; }

    // ── Payment Method ──────────────────────────────────────────────────

    public VendorPaymentMethod PaymentMethod { get; set; } = VendorPaymentMethod.BankTransfer;
    public Guid? PaymentMethodId { get; set; }
    public virtual PaymentMethod? ConfiguredPaymentMethod { get; set; }

    public Guid? BankAccountId { get; set; }
    public virtual BankAccount? BankAccount { get; set; }

    // ── Status & Authorization ──────────────────────────────────────────

    public PaymentBatchStatus Status { get; set; } = PaymentBatchStatus.Draft;

    public bool ApprovalRequired { get; set; } = true;

    public Guid? CreatedById { get; set; }
    public Guid? ApprovedById { get; set; }
    public DateTime? ApprovedDate { get; set; }

    /// <summary>
    /// Immutable AP-004/TDC-0506 decision for the exact batch invoice set.
    /// </summary>
    public Guid? InvoicePaymentSodControlEventId { get; set; }
    public virtual ProcurementControlEvent? InvoicePaymentSodControlEvent { get; set; }

    public Guid? ProcessedById { get; set; }
    public DateTime? ProcessedDate { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    // ── Multi-tenant ────────────────────────────────────────────────────

    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;

    // ── Navigation ──────────────────────────────────────────────────────

    public virtual ICollection<PaymentBatchItem> Items { get; set; } = new List<PaymentBatchItem>();
    public virtual ICollection<PaymentBatchInvoice> Invoices { get; set; } = new List<PaymentBatchInvoice>();
}

/// <summary>
/// Links a VendorPayment to a PaymentBatch.
/// </summary>
public class PaymentBatchItem : TenantEntity
{
    [Required]
    public Guid PaymentBatchId { get; set; }
    public virtual PaymentBatch PaymentBatch { get; set; } = null!;

    [Required]
    public Guid VendorPaymentId { get; set; }
    public virtual VendorPayment VendorPayment { get; set; } = null!;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [MaxLength(50)]
    public string ItemStatus { get; set; } = "Pending"; // Pending, Processed, Failed

    [MaxLength(500)]
    public string? FailureReason { get; set; }

    // ── Multi-tenant ────────────────────────────────────────────────────

    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;

    public virtual ICollection<PaymentBatchInvoice> Invoices { get; set; } = new List<PaymentBatchInvoice>();
}

/// <summary>
/// Controlled AP-006/TDC-0507 exception lifecycle for a specific immutable
/// three-way-match snapshot.
/// </summary>
public enum VendorInvoiceMatchExceptionStatus
{
    PendingApproval = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4,
    Expired = 5
}

public enum VendorInvoiceMatchExceptionEvidenceKind
{
    WorkflowEvidenceDocument = 1,
    CentralDocument = 2
}

public enum VendorInvoiceMatchCorrectiveActionStatus
{
    Planned = 1,
    Completed = 2
}

/// <summary>
/// Locks a payment batch to the exact invoice set selected at creation. Batch
/// approval and processing revalidate these rows and never discover additional
/// supplier invoices dynamically.
/// </summary>
public class PaymentBatchInvoice : TenantEntity
{
    [Required]
    public Guid PaymentBatchId { get; set; }
    public virtual PaymentBatch PaymentBatch { get; set; } = null!;

    [Required]
    public Guid PaymentBatchItemId { get; set; }
    public virtual PaymentBatchItem PaymentBatchItem { get; set; } = null!;

    [Required]
    public Guid VendorPaymentId { get; set; }
    public virtual VendorPayment VendorPayment { get; set; } = null!;

    [Required]
    public Guid VendorInvoiceId { get; set; }
    public virtual VendorInvoice VendorInvoice { get; set; } = null!;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = "Pending";

    [MaxLength(500)]
    public string? FailureReason { get; set; }

    public Guid PaymentReadinessControlEventId { get; set; }
    public virtual ProcurementControlEvent PaymentReadinessControlEvent { get; set; } = null!;

    [Required, MaxLength(64)]
    public string PaymentReadinessSnapshotHash { get; set; } = string.Empty;

    public DateTime PaymentReadinessEvaluatedAtUtc { get; set; }

    public Guid TenantId { get; set; }
    public virtual Tenant Tenant { get; set; } = null!;
}

#endregion

#region Vendor Invoice Match Exceptions

/// <summary>
/// Finance-owned exception request that produces the narrow AP-006 control
/// event already consumed by the mandatory match and payment-readiness gates.
/// It does not allocate or post a payment.
/// </summary>
[Table("VendorInvoiceMatchException")]
public class VendorInvoiceMatchException : TenantEntity
{
    public Guid VendorInvoiceId { get; set; }
    public virtual VendorInvoice VendorInvoice { get; set; } = null!;
    public Guid PurchaseOrderId { get; set; }
    public virtual PurchaseOrder PurchaseOrder { get; set; } = null!;
    [Range(1, int.MaxValue)] public int Sequence { get; set; }
    public VendorInvoiceMatchExceptionStatus Status { get; set; } =
        VendorInvoiceMatchExceptionStatus.PendingApproval;

    [Required, MaxLength(200)] public string VarianceType { get; set; } = string.Empty;
    [Column(TypeName = "decimal(5,2)")] public decimal PriceTolerancePercent { get; set; }
    [Column(TypeName = "decimal(5,2)")] public decimal QuantityTolerancePercent { get; set; }
    [Required, MaxLength(100)] public string RootCauseCategory { get; set; } = string.Empty;
    [Required, MaxLength(2000)] public string RootCauseDescription { get; set; } = string.Empty;
    [Required, MaxLength(2000)] public string Justification { get; set; } = string.Empty;
    [Required, MaxLength(2000)] public string CorrectiveAction { get; set; } = string.Empty;
    public Guid CorrectiveActionOwnerId { get; set; }
    [Required, MaxLength(300)] public string CorrectiveActionOwnerName { get; set; } = string.Empty;
    public DateTime CorrectiveActionDueAtUtc { get; set; }
    public VendorInvoiceMatchCorrectiveActionStatus CorrectiveActionStatus { get; set; } =
        VendorInvoiceMatchCorrectiveActionStatus.Planned;
    public DateTime? CorrectiveActionCompletedAtUtc { get; set; }
    public Guid? CorrectiveActionCompletedById { get; set; }
    [MaxLength(2000)] public string? CorrectiveActionCompletionNote { get; set; }

    public DateTime ExpiresAtUtc { get; set; }
    [Required, MaxLength(64)] public string InvoiceSnapshotHash { get; set; } = string.Empty;
    [Required, Column(TypeName = "nvarchar(max)")] public string VarianceSnapshotJson { get; set; } = "[]";
    public Guid? ConfigurationProfileId { get; set; }
    public int? ConfigurationProfileVersion { get; set; }
    public Guid? WorkflowInstanceId { get; set; }
    public virtual WorkflowInstance? WorkflowInstance { get; set; }
    public Guid RequestedById { get; set; }
    [Required, MaxLength(300)] public string RequestedByName { get; set; } = string.Empty;
    public DateTime RequestedAtUtc { get; set; }
    public Guid? FinalApprovedById { get; set; }
    [MaxLength(300)] public string? FinalApprovedByName { get; set; }
    public DateTime? FinalApprovedAtUtc { get; set; }
    public Guid? RejectedById { get; set; }
    [MaxLength(300)] public string? RejectedByName { get; set; }
    public DateTime? RejectedAtUtc { get; set; }
    [MaxLength(2000)] public string? DecisionComment { get; set; }
    public Guid? ApprovalControlEventId { get; set; }
    public virtual ProcurementControlEvent? ApprovalControlEvent { get; set; }
    [Required, MaxLength(100)] public string IdempotencyKey { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string CorrelationId { get; set; } = string.Empty;
    [Required, MaxLength(64)] public string IntegrityHash { get; set; } = string.Empty;
    [Timestamp] public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public virtual ICollection<VendorInvoiceMatchExceptionVariance> Variances { get; set; } =
        new List<VendorInvoiceMatchExceptionVariance>();
    public virtual ICollection<VendorInvoiceMatchExceptionEvidence> Evidence { get; set; } =
        new List<VendorInvoiceMatchExceptionEvidence>();
    public virtual ICollection<VendorInvoiceMatchExceptionAction> Actions { get; set; } =
        new List<VendorInvoiceMatchExceptionAction>();
}

[Table("VendorInvoiceMatchExceptionVariance")]
public class VendorInvoiceMatchExceptionVariance : TenantEntity
{
    public Guid MatchExceptionId { get; set; }
    public virtual VendorInvoiceMatchException MatchException { get; set; } = null!;
    [Required, MaxLength(100)] public string VarianceType { get; set; } = string.Empty;
    [Required, MaxLength(500)] public string ItemDescription { get; set; } = string.Empty;
    [Column(TypeName = "decimal(18,4)")] public decimal ActualValue { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal ExpectedValue { get; set; }
    [Column(TypeName = "decimal(18,4)")] public decimal Variance { get; set; }
    [Column(TypeName = "decimal(9,4)")] public decimal VariancePercentage { get; set; }
    [Column(TypeName = "decimal(5,2)")] public decimal ConfiguredTolerancePercent { get; set; }
}

[Table("VendorInvoiceMatchExceptionEvidence")]
public class VendorInvoiceMatchExceptionEvidence : TenantEntity
{
    public Guid MatchExceptionId { get; set; }
    public virtual VendorInvoiceMatchException MatchException { get; set; } = null!;
    [Required, MaxLength(100)] public string RequirementKey { get; set; } = string.Empty;
    public VendorInvoiceMatchExceptionEvidenceKind ReferenceKind { get; set; }
    public Guid? WorkflowEvidenceDocumentId { get; set; }
    public virtual WorkflowEvidenceDocument? WorkflowEvidenceDocument { get; set; }
    public Guid? FileUploadRecordId { get; set; }
    public virtual FileUploadRecord? FileUploadRecord { get; set; }
    [Required, MaxLength(1000)] public string EvidenceReference { get; set; } = string.Empty;
    [Required, MaxLength(64)] public string EvidenceHash { get; set; } = string.Empty;
}

[Table("VendorInvoiceMatchExceptionAction")]
public class VendorInvoiceMatchExceptionAction : TenantEntity
{
    public Guid MatchExceptionId { get; set; }
    public virtual VendorInvoiceMatchException MatchException { get; set; } = null!;
    [Range(1, int.MaxValue)] public int Sequence { get; set; }
    [Required, MaxLength(100)] public string Action { get; set; } = string.Empty;
    public VendorInvoiceMatchExceptionStatus FromStatus { get; set; }
    public VendorInvoiceMatchExceptionStatus ToStatus { get; set; }
    public Guid ActorUserId { get; set; }
    [Required, MaxLength(300)] public string ActorName { get; set; } = string.Empty;
    [Required, MaxLength(2000)] public string Comment { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
    [Required, MaxLength(64)] public string IntegrityHash { get; set; } = string.Empty;
}

#endregion
