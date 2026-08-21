using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// Core double-entry transaction line item entity.
/// Supports segmented accounts, multi-currency, parallel books (IFRS/Local Statutory/Management),
/// and comprehensive audit trails per Finance Module Enhanced Workflows.
/// </summary>
public class AccountTransaction : BusinessEntity
{
    // ========================================================================
    // CORE TRANSACTION DETAILS
    // ========================================================================
    
    /// <summary>
    /// Reference to the GL Account being debited or credited.
    /// Links to Account with full segment structure.
    /// </summary>
    [Required]
    public Guid AccountId { get; set; }

    /// <summary>
    /// Reference to the parent Journal Entry (voucher/header).
    /// All transactions must belong to a journal entry for balanced postings.
    /// </summary>
    [Required]
    public Guid JournalEntryId { get; set; }

    /// <summary>
    /// Transaction date (posting date to GL).
    /// Must fall within an open fiscal period.
    /// </summary>
    [Required]
    public DateTime TransactionDate { get; set; }

    /// <summary>
    /// Line-level description/narrative for this transaction line.
    /// Supplements the journal entry header description.
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    // ========================================================================
    // DEBIT/CREDIT AMOUNTS (BASE CURRENCY)
    // ========================================================================
    
    /// <summary>
    /// Debit amount in base currency (home currency).
    /// Either DebitAmount or CreditAmount must be > 0, not both.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal DebitAmount { get; set; } = 0;

    /// <summary>
    /// Credit amount in base currency (home currency).
    /// Either DebitAmount or CreditAmount must be > 0, not both.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal CreditAmount { get; set; } = 0;

    // ========================================================================
    // MULTI-CURRENCY SUPPORT (Section 3.2.4)
    // ========================================================================

    /// <summary>
    /// Tenant functional currency used to measure this posted line.
    /// DebitAmount and CreditAmount are expressed in this currency.
    /// </summary>
    [Required]
    [MaxLength(3)]
    public string FunctionalCurrencyCode { get; set; } = "GHS";
    
    /// <summary>
    /// Currency code for this transaction.
    /// If NULL or matches base currency, this is a base currency transaction.
    /// If different, this is a foreign currency transaction requiring conversion.
    /// </summary>
    [MaxLength(3)]
    public string? TransactionCurrency { get; set; }

    /// <summary>
    /// Debit amount in the original transaction currency.
    /// For functional-currency transactions this normally matches DebitAmount.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? TransactionDebitAmount { get; set; }

    /// <summary>
    /// Credit amount in the original transaction currency.
    /// For functional-currency transactions this normally matches CreditAmount.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? TransactionCreditAmount { get; set; }

    /// <summary>
    /// Foreign currency amount (if TransactionCurrency is not base currency).
    /// This is the original transaction amount before conversion.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? ForeignCurrencyAmount { get; set; }

    /// <summary>
    /// Exchange rate used for currency conversion.
    /// NULL if base currency transaction.
    /// Format: 1 Foreign Currency = X Base Currency units
    /// Example: 1 USD = 15.25 GHS
    /// </summary>
    [Column(TypeName = "decimal(18,6)")]
    public decimal? ExchangeRate { get; set; }

    /// <summary>
    /// Tenant-owned exchange-rate record used for this line, when foreign currency applies.
    /// This preserves the exact rate source independently of later rate edits.
    /// </summary>
    public Guid? ExchangeRateId { get; set; }

    /// <summary>
    /// Source of exchange rate used.
    /// Examples: "Bank of Ghana", "Manual Entry", "Bloomberg", "Automatic-Daily Rate"
    /// </summary>
    [MaxLength(100)]
    public string? ExchangeRateSource { get; set; }

    /// <summary>
    /// Date when exchange rate was effective/retrieved.
    /// Used for historical rate tracking and validation.
    /// </summary>
    public DateTime? ExchangeRateDate { get; set; }

    [ForeignKey(nameof(ExchangeRateId))]
    public virtual ExchangeRate? ExchangeRateRecord { get; set; }

    // ========================================================================
    // SOURCE DOCUMENT TRACKING
    // ========================================================================
    
    /// <summary>
    /// Source module that generated this transaction.
    /// Examples: "GL" (manual entry), "AP", "AR", "FA" (Fixed Assets), 
    /// "INV" (Inventory), "ESTATE", "PAYROLL"
    /// </summary>
    [MaxLength(50)]
    public string? SourceModule { get; set; }

    /// <summary>
    /// Source document/transaction ID from originating module.
    /// Example: Invoice ID from AR, Payment ID from AP, etc.
    /// Enables drill-down from GL to source transaction.
    /// </summary>
    public Guid? SourceDocumentId { get; set; }

    /// <summary>
    /// Immutable line-level origin within the source document. Operational modules retain their
    /// source records while Finance uses this lineage for exact corrective postings and audit.
    /// </summary>
    public Guid? SourceDocumentLineId { get; set; }

    /// <summary>
    /// Source document type for reference and reporting.
    /// Examples: "Invoice", "Payment", "Journal Entry", "Asset Depreciation"
    /// </summary>
    [MaxLength(100)]
    public string? SourceDocumentType { get; set; }

    /// <summary>
    /// Source document reference number (e.g., Invoice Number, PO Number).
    /// Human-readable reference for audit and investigation.
    /// </summary>
    [MaxLength(100)]
    public string? SourceReferenceNumber { get; set; }

    // ========================================================================
    // CLASSIFICATION SUPPORT (Section 3.2.6 - IFRS / LOCAL_STATUTORY / MANAGEMENT)
    // ========================================================================
    
    /// <summary>
    /// Book/Classification for parallel accounting frameworks.
    /// Values: "IFRS", "LOCAL_STATUTORY", "MANAGEMENT"
    /// Enables separate GL for IFRS reporting, statutory reporting, and tax reporting.
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string BookClassification { get; set; } = "IFRS";

    // ========================================================================
    // PERIOD MANAGEMENT AND POSTING CONTROLS
    // ========================================================================
    
    /// <summary>
    /// Reference to the fiscal period this transaction belongs to.
    /// System validates period is open before allowing posting.
    /// </summary>
    [Required]
    public Guid FiscalPeriodId { get; set; }

    /// <summary>
    /// Actual date when transaction was posted to GL.
    /// May differ from TransactionDate (e.g., backdated entries).
    /// </summary>
    public DateTime? PostedDate { get; set; }

    /// <summary>
    /// Posting status of this transaction line.
    /// Values: "Draft", "Posted", "Reversed"
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string PostingStatus { get; set; } = "Draft";

    // ========================================================================
    // REVERSAL TRACKING (For journal entry corrections and revaluation reversals)
    // ========================================================================
    
    /// <summary>
    /// Flag indicating if this transaction has been reversed.
    /// Set to true when a reversal entry is posted.
    /// </summary>
    public bool IsReversed { get; set; } = false;

    /// <summary>
    /// Date when this transaction was reversed.
    /// NULL if not reversed.
    /// </summary>
    public DateTime? ReversalDate { get; set; }

    /// <summary>
    /// Reference to the reversal transaction (if this was reversed).
    /// Links to the AccountTransaction that reversed this entry.
    /// </summary>
    public Guid? ReversalTransactionId { get; set; }

    /// <summary>
    /// Reference to the original transaction (if this is a reversal entry).
    /// Links to the AccountTransaction being reversed by this entry.
    /// </summary>
    public Guid? OriginalTransactionId { get; set; }

    /// <summary>
    /// Reversal type: "Manual" (user-initiated), "Automatic" (system-generated for revaluations)
    /// </summary>
    [MaxLength(20)]
    public string? ReversalType { get; set; }

    /// <summary>
    /// Reason/justification for reversal.
    /// Mandatory when IsReversed = true.
    /// </summary>
    [MaxLength(500)]
    public string? ReversalReason { get; set; }

    // ========================================================================
    // SEGMENT TRACKING (For reporting and analysis)
    // ========================================================================
    
    /// <summary>
    /// Cached segment string for performance and reporting.
    /// Example: "001-FIN-1000-01" for quick segment-based filtering.
    /// Populated from Account's segment values at posting time.
    /// </summary>
    [MaxLength(200)]
    public string? SegmentString { get; set; }

    // ========================================================================
    // REVALUATION TRACKING (Section 3.2.5 - IAS 21 Currency Revaluation)
    // ========================================================================
    
    /// <summary>
    /// Flag indicating if this is a currency revaluation entry.
    /// True for automatic revaluation entries generated by system.
    /// </summary>
    public bool IsRevaluationEntry { get; set; } = false;

    /// <summary>
    /// Revaluation batch reference number.
    /// Groups all revaluation entries from a single revaluation run.
    /// Example: "REVAL-2025-01-001"
    /// </summary>
    [MaxLength(50)]
    public string? RevaluationBatchNumber { get; set; }

    /// <summary>
    /// Revaluation type: "Unrealized" (period-end revaluation), "Realized" (settlement)
    /// </summary>
    [MaxLength(20)]
    public string? RevaluationType { get; set; }

    // ========================================================================
    // ADDITIONAL TRACKING AND NOTES
    // ========================================================================
    
    /// <summary>
    /// Line number within the journal entry for ordering.
    /// Maintains transaction line sequence.
    /// </summary>
    public int LineNumber { get; set; }

    /// <summary>
    /// Additional notes or comments for this transaction line.
    /// Used for explanations, audit notes, or special instructions.
    /// </summary>
    [MaxLength(1000)]
    public string? Notes { get; set; }

    /// <summary>
    /// Tag or category for transaction classification.
    /// Examples: "Adjustment", "Accrual", "Deferral", "Reclassification"
    /// </summary>
    [MaxLength(50)]
    public string? TransactionTag { get; set; }

    // ========================================================================
    // NAVIGATION PROPERTIES
    // ========================================================================
    
    /// <summary>
    /// The GL Account being debited or credited.
    /// Includes full segment structure and currency configuration.
    /// </summary>
    [ForeignKey(nameof(AccountId))]
    public virtual Account Account { get; set; } = null!;

    /// <summary>
    /// The parent Journal Entry (voucher/header).
    /// Provides transaction grouping and balanced entry control.
    /// </summary>
    [ForeignKey(nameof(JournalEntryId))]
    public virtual JournalEntry JournalEntry { get; set; } = null!;

    /// <summary>
    /// The fiscal period this transaction belongs to.
    /// Used for period locking and financial reporting.
    /// </summary>
    [ForeignKey(nameof(FiscalPeriodId))]
    public virtual FiscalPeriod FiscalPeriod { get; set; } = null!;

    /// <summary>
    /// The reversal transaction (if this transaction was reversed).
    /// Enables navigation from original to reversal entry.
    /// </summary>
    [ForeignKey(nameof(ReversalTransactionId))]
    public virtual AccountTransaction? ReversalTransaction { get; set; }

    /// <summary>
    /// The original transaction (if this is a reversal entry).
    /// Enables navigation from reversal to original entry.
    /// </summary>
    [ForeignKey(nameof(OriginalTransactionId))]
    public virtual AccountTransaction? OriginalTransaction { get; set; }
}
