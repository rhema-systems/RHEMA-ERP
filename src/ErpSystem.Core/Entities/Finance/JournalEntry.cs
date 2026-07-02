using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// Journal Entry header/voucher entity for double-entry bookkeeping.
/// Groups AccountTransaction lines into balanced entries with approval workflow support.
/// Implements maker-checker controls via external approval API per Enhanced Workflows.
/// </summary>
public class JournalEntry : BusinessEntity
{
    // ========================================================================
    // CORE JOURNAL ENTRY IDENTIFICATION
    // ========================================================================
    
    /// <summary>
    /// System-generated journal entry number (sequential).
    /// Format: JE-YYYY-NNNNN (e.g., JE-2025-00001)
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string JournalEntryNumber { get; set; } = string.Empty;

    /// <summary>
    /// Journal entry type classification.
    /// Values: "General", "Adjusting", "Reversing", "Recurring", "Opening Balance",
    /// "Closing", "Revaluation", "System Generated"
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string JournalType { get; set; } = "General";

    /// <summary>
    /// Entry date (transaction date for the journal entry).
    /// All transaction lines inherit this date unless overridden.
    /// Must fall within an open fiscal period.
    /// </summary>
    [Required]
    public DateTime EntryDate { get; set; }

    /// <summary>
    /// Overall description/narrative for the journal entry.
    /// Provides context for the entire transaction voucher.
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// External reference number (e.g., invoice number, PO number, contract reference).
    /// Links journal entry to source documents outside the GL.
    /// </summary>
    [MaxLength(100)]
    public string? ReferenceNumber { get; set; }

    // ========================================================================
    // SOURCE DOCUMENT AND MODULE TRACKING
    // ========================================================================
    
    /// <summary>
    /// Source module that generated this journal entry.
    /// Examples: "GL" (manual), "AP", "AR", "FA", "INV", "ESTATE", "PAYROLL"
    /// </summary>
    [MaxLength(50)]
    public string? SourceModule { get; set; }

    /// <summary>
    /// Source document ID from originating module.
    /// Enables drill-down from GL to source transaction.
    /// </summary>
    public Guid? SourceDocumentId { get; set; }

    /// <summary>
    /// Source document type for reference.
    /// Examples: "Invoice", "Payment", "Depreciation", "Salary Journal"
    /// </summary>
    [MaxLength(100)]
    public string? SourceDocumentType { get; set; }

    // ========================================================================
    // AMOUNTS AND BALANCE VALIDATION
    // ========================================================================
    
    /// <summary>
    /// Total debit amount for all transaction lines (base currency).
    /// Must equal TotalCreditAmount for balanced entry.
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalDebitAmount { get; set; } = 0;

    /// <summary>
    /// Total credit amount for all transaction lines (base currency).
    /// Must equal TotalDebitAmount for balanced entry.
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalCreditAmount { get; set; } = 0;

    /// <summary>
    /// Calculated difference between debits and credits.
    /// Should always be 0.00 for valid entries.
    /// Non-zero indicates out-of-balance condition requiring correction.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal BalanceDifference { get; set; } = 0;

    /// <summary>
    /// Flag indicating whether entry is in balance.
    /// True when TotalDebitAmount = TotalCreditAmount (within tolerance)
    /// </summary>
    [Required]
    public bool IsBalanced { get; set; } = false;

    // ========================================================================
    // MULTI-CURRENCY SUPPORT
    // ========================================================================
    
    /// <summary>
    /// Indicates if this journal entry contains multi-currency transactions.
    /// True if any transaction line uses foreign currency.
    /// </summary>
    public bool IsMultiCurrency { get; set; } = false;

    /// <summary>
    /// Primary transaction currency (if all lines use same foreign currency).
    /// NULL for mixed currency or base currency only entries.
    /// </summary>
    [MaxLength(3)]
    public string? PrimaryCurrency { get; set; }

    // ========================================================================
    // CLASSIFICATION SUPPORT (IFRS / LOCAL_STATUTORY / MANAGEMENT)
    // ========================================================================
    
    /// <summary>
    /// Book classification for parallel accounting frameworks.
    /// Values: "IFRS", "LOCAL_STATUTORY", "MANAGEMENT"
    /// All transaction lines inherit this classification.
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string BookClassification { get; set; } = "IFRS";

    // ========================================================================
    // PERIOD MANAGEMENT
    // ========================================================================
    
    /// <summary>
    /// Reference to fiscal period this entry belongs to.
    /// System validates period is open before allowing posting.
    /// </summary>
    [Required]
    public Guid FiscalPeriodId { get; set; }

    /// <summary>
    /// Posting date (actual date when entry was posted to GL).
    /// May differ from EntryDate for backdated or future-dated entries.
    /// </summary>
    public DateTime? PostingDate { get; set; }

    /// <summary>
    /// User who posted the journal entry.
    /// Captured separately from CreatedBy for audit purposes.
    /// </summary>
    public Guid? PostedByUserId { get; set; }

    // ========================================================================
    // POSTING STATUS AND WORKFLOW
    // ========================================================================
    
    /// <summary>
    /// Current posting status of the journal entry.
    /// Values: "Draft", "Pending Approval", "Approved", "Posted", "Rejected", "Reversed"
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string PostingStatus { get; set; } = "Draft";

    /// <summary>
    /// Flag indicating if this entry requires approval before posting.
    /// Based on entry amount, type, or user authority limits.
    /// </summary>
    [Required]
    public bool RequiresApproval { get; set; } = false;

    /// <summary>
    /// Approval status from external approval workflow API.
    /// Values: "Not Required", "Pending", "Approved", "Rejected"
    /// </summary>
    [MaxLength(50)]
    public string? ApprovalStatus { get; set; }

    /// <summary>
    /// External approval workflow ID from approval system.
    /// Links to external approval API for maker-checker workflow.
    /// </summary>
    [MaxLength(100)]
    public string? ApprovalWorkflowId { get; set; }

    /// <summary>
    /// User who approved the journal entry.
    /// Populated from external approval API response.
    /// </summary>
    public Guid? ApprovedByUserId { get; set; }

    /// <summary>
    /// Date and time when approval was granted.
    /// </summary>
    public DateTime? ApprovedDate { get; set; }

    /// <summary>
    /// Rejection reason if entry was rejected.
    /// Captured from approval workflow.
    /// </summary>
    [MaxLength(1000)]
    public string? RejectionReason { get; set; }

    // ========================================================================
    // REVERSAL TRACKING
    // ========================================================================
    
    /// <summary>
    /// Flag indicating if this journal entry has been reversed.
    /// Set to true when a reversal entry is created and posted.
    /// </summary>
    public bool IsReversed { get; set; } = false;

    /// <summary>
    /// Date when this entry was reversed.
    /// NULL if not reversed.
    /// </summary>
    public DateTime? ReversalDate { get; set; }

    /// <summary>
    /// Reference to the reversal journal entry (if this was reversed).
    /// Links to the JournalEntry that reversed this entry.
    /// </summary>
    public Guid? ReversalJournalEntryId { get; set; }

    /// <summary>
    /// Reference to the original journal entry (if this is a reversal).
    /// Links to the JournalEntry being reversed by this entry.
    /// </summary>
    public Guid? OriginalJournalEntryId { get; set; }

    /// <summary>
    /// Reversal type: "Manual" (user-initiated), "Automatic" (system-generated)
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
    // RECURRING JOURNAL ENTRY SUPPORT
    // ========================================================================
    
    /// <summary>
    /// Flag indicating if this is a recurring journal entry template.
    /// True for entries that auto-generate periodically.
    /// </summary>
    public bool IsRecurring { get; set; } = false;

    /// <summary>
    /// Reference to recurring journal template (if generated from template).
    /// Links to the master recurring entry definition.
    /// </summary>
    public Guid? RecurringTemplateId { get; set; }

    /// <summary>
    /// Recurrence frequency: "Monthly", "Quarterly", "Annually"
    /// NULL if not recurring.
    /// </summary>
    [MaxLength(20)]
    public string? RecurrenceFrequency { get; set; }

    /// <summary>
    /// Next scheduled generation date for recurring entry.
    /// System uses this to auto-generate next occurrence.
    /// </summary>
    public DateTime? NextRecurrenceDate { get; set; }

    // ========================================================================
    // CURRENCY REVALUATION SPECIFIC (Section 3.2.5)
    // ========================================================================
    
    /// <summary>
    /// Flag indicating if this is a currency revaluation entry.
    /// True for automatic revaluation entries per IAS 21.
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
    /// Revaluation type: "Unrealized" (period-end), "Realized" (settlement)
    /// </summary>
    [MaxLength(20)]
    public string? RevaluationType { get; set; }

    /// <summary>
    /// Flag indicating if this is an automatic reversal of prior revaluation.
    /// Per IAS 21 workflow, prior period revaluations auto-reverse.
    /// </summary>
    public bool IsAutoReversalEntry { get; set; } = false;

    // ========================================================================
    // ADDITIONAL CONTROLS AND NOTES
    // ========================================================================
    
    /// <summary>
    /// Flag indicating if entry was imported from external system.
    /// </summary>
    public bool IsImported { get; set; } = false;

    /// <summary>
    /// Import batch reference if entry was imported.
    /// </summary>
    [MaxLength(100)]
    public string? ImportBatchReference { get; set; }

    /// <summary>
    /// Additional notes or comments for the journal entry.
    /// Used for explanations, audit notes, or special instructions.
    /// </summary>
    [MaxLength(2000)]
    public string? Notes { get; set; }

    /// <summary>
    /// Tag or category for entry classification and filtering.
    /// Examples: "Period End", "Depreciation", "Accrual", "Year End Close"
    /// </summary>
    [MaxLength(100)]
    public string? EntryTag { get; set; }

    /// <summary>
    /// Priority level for processing (if queued for posting).
    /// Higher numbers = higher priority.
    /// </summary>
    public int Priority { get; set; } = 0;

    // ========================================================================
    // ATTACHMENT SUPPORT
    // ========================================================================
    
    /// <summary>
    /// Flag indicating if supporting documents are attached.
    /// </summary>
    public bool HasAttachments { get; set; } = false;

    /// <summary>
    /// Count of attached documents.
    /// </summary>
    public int AttachmentCount { get; set; } = 0;

    // ========================================================================
    // NAVIGATION PROPERTIES
    // ========================================================================
    
    /// <summary>
    /// Collection of transaction lines belonging to this journal entry.
    /// All lines must balance (total debits = total credits).
    /// </summary>
    public virtual ICollection<AccountTransaction> Transactions { get; set; } = new List<AccountTransaction>();

    /// <summary>
    /// Collection of attachments linked to this journal entry.
    /// </summary>
    public virtual ICollection<JournalEntryAttachment> Attachments { get; set; } = new List<JournalEntryAttachment>();

    /// <summary>
    /// The fiscal period this entry belongs to.
    /// Used for period locking and financial reporting.
    /// </summary>
    [ForeignKey(nameof(FiscalPeriodId))]
    public virtual FiscalPeriod FiscalPeriod { get; set; } = null!;

    /// <summary>
    /// The reversal journal entry (if this entry was reversed).
    /// Enables navigation from original to reversal entry.
    /// </summary>
    [ForeignKey(nameof(ReversalJournalEntryId))]
    public virtual JournalEntry? ReversalJournalEntry { get; set; }

    /// <summary>
    /// The original journal entry (if this is a reversal entry).
    /// Enables navigation from reversal to original entry.
    /// </summary>
    [ForeignKey(nameof(OriginalJournalEntryId))]
    public virtual JournalEntry? OriginalJournalEntry { get; set; }
}

/// <summary>
/// Link between a Journal Entry and a FileUploadRecord.
/// </summary>
[Table("JournalEntryAttachments")]
public class JournalEntryAttachment : TenantEntity
{
    public Guid JournalEntryId { get; set; }
    
    [ForeignKey(nameof(JournalEntryId))]
    public virtual JournalEntry JournalEntry { get; set; } = null!;

    public Guid FileUploadRecordId { get; set; }
    
    [ForeignKey(nameof(FileUploadRecordId))]
    public virtual FileUploadRecord FileUploadRecord { get; set; } = null!;
}
