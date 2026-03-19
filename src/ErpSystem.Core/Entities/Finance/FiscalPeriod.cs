using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;
using ErpSystem.Core.Enums;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// Fiscal Period entity for period management and financial controls.
/// Implements period locking, closing procedures, and prevents backdated transactions.
/// Supports month-end close workflow per Section 3.2.X.9 Enhanced Workflows.
/// </summary>
public class FiscalPeriod : BusinessEntity
{
    // ========================================================================
    // CORE PERIOD IDENTIFICATION
    // ========================================================================
    
    /// <summary>
    /// Reference to the fiscal year this period belongs to.
    /// Links to FiscalYear for year-level management.
    /// </summary>
    [Required]
    public Guid FiscalYearId { get; set; }

    /// <summary>
    /// Period name/identifier.
    /// Examples: "January 2025", "Q1-2025", "Period 01-2025"
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string PeriodName { get; set; } = string.Empty;

    /// <summary>
    /// Period code for system reference.
    /// Format: YYYY-MM or YYYY-QQ
    /// Examples: "2025-01", "2025-Q1"
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string PeriodCode { get; set; } = string.Empty;

    /// <summary>
    /// Period number within the fiscal year (1-366 for daily).
    /// </summary>
    [Required]
    [Range(1, 366)]
    public int PeriodNumber { get; set; }

    /// <summary>
    /// Period type classification.
    /// Values: Monthly, Quarterly, Weekly, Daily
    /// </summary>
    [Required]
    public PeriodType PeriodType { get; set; } = PeriodType.Monthly;

    // ========================================================================
    // PERIOD DATE RANGE
    // ========================================================================
    
    /// <summary>
    /// Period start date (first day of period).
    /// Transactions with TransactionDate >= StartDate can be posted to this period.
    /// </summary>
    [Required]
    public DateTime StartDate { get; set; }

    /// <summary>
    /// Period end date (last day of period).
    /// Transactions with TransactionDate <= EndDate can be posted to this period.
    /// </summary>
    [Required]
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Number of days in this period.
    /// Calculated: (EndDate - StartDate).Days + 1
    /// </summary>
    public int PeriodDays { get; set; }

    // ========================================================================
    // PERIOD STATUS AND CONTROLS
    // ========================================================================
    
    /// <summary>
    /// Current status of the fiscal period.
    /// Values: "Future", "Open", "Closed", "Locked"
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string PeriodStatus { get; set; } = "Future";

    /// <summary>
    /// Flag indicating if period allows transaction posting.
    /// False prevents any new transactions in this period.
    /// </summary>
    [Required]
    public bool IsOpen { get; set; } = false;

    /// <summary>
    /// Flag indicating if period is permanently locked (Global/Finance Lock).
    /// If True, period is locked for ALL modules regardless of module-level locks.
    /// Implements Finance-first locking hierarchy.
    /// </summary>
    public bool IsLocked { get; set; } = false;

    /// <summary>
    /// Helper property to distinguish Finance lock from global lock if needed,
    /// but generally IsLocked handles the "Master Lock" (Finance Lock).
    /// </summary>
    public bool IsFinanceLocked => IsLocked;

    /// <summary>
    /// Date when period was locked.
    /// NULL if never locked.
    /// </summary>
    public DateTime? LockedDate { get; set; }

    /// <summary>
    /// User who locked the period.
    /// Captured for audit trail.
    /// </summary>
    public Guid? LockedByUserId { get; set; }

    /// <summary>
    /// Reason for locking the period.
    /// Example: "Year-end close completed and audited", "External audit complete"
    /// </summary>
    [MaxLength(500)]
    public string? LockReason { get; set; }

    // ========================================================================
    // PERIOD CLOSE WORKFLOW (Section 3.2.X.9)
    // ========================================================================
    
    /// <summary>
    /// Flag indicating if period close process has been initiated.
    /// Set to true when user starts period-end close procedures.
    /// </summary>
    public bool IsCloseInitiated { get; set; } = false;

    /// <summary>
    /// Date when period close was initiated.
    /// </summary>
    public DateTime? CloseInitiatedDate { get; set; }

    /// <summary>
    /// User who initiated the close process.
    /// </summary>
    public Guid? CloseInitiatedByUserId { get; set; }

    /// <summary>
    /// Flag indicating if period has been successfully closed.
    /// True when all close checklist items completed and validated.
    /// </summary>
    public bool IsClosed { get; set; } = false;

    /// <summary>
    /// Date when period was officially closed.
    /// </summary>
    public DateTime? ClosedDate { get; set; }

    /// <summary>
    /// User who closed the period.
    /// </summary>
    public Guid? ClosedByUserId { get; set; }

    // ========================================================================
    // PRE-CLOSE CHECKLIST VALIDATION
    // ========================================================================
    
    /// <summary>
    /// Flag indicating if trial balance validation passed.
    /// Validates Debits = Credits for all transactions in period.
    /// </summary>
    public bool TrialBalanceValidated { get; set; } = false;

    /// <summary>
    /// Date when trial balance validation was performed.
    /// </summary>
    public DateTime? TrialBalanceValidatedDate { get; set; }

    /// <summary>
    /// Flag indicating if bank reconciliation is complete for all accounts.
    /// Ensures all bank transactions reconciled before close.
    /// </summary>
    public bool BankReconciliationComplete { get; set; } = false;

    /// <summary>
    /// Date when bank reconciliation completion was verified.
    /// </summary>
    public DateTime? BankReconciliationCompletedDate { get; set; }

    /// <summary>
    /// Flag indicating if currency revaluation has been performed (if required).
    /// Per IAS 21, foreign currency accounts revalued at period-end.
    /// </summary>
    public bool CurrencyRevaluationComplete { get; set; } = false;

    /// <summary>
    /// Date when currency revaluation was performed.
    /// </summary>
    public DateTime? CurrencyRevaluationDate { get; set; }

    /// <summary>
    /// Flag indicating if depreciation has been run for the period (if applicable).
    /// Ensures fixed asset depreciation posted before close.
    /// </summary>
    public bool DepreciationComplete { get; set; } = false;

    /// <summary>
    /// Date when depreciation was run and posted.
    /// </summary>
    public DateTime? DepreciationCompletedDate { get; set; }

    /// <summary>
    /// Flag indicating if inventory valuation has been performed (if applicable).
    /// Ensures inventory properly valued at period-end.
    /// </summary>
    public bool InventoryValuationComplete { get; set; } = false;

    /// <summary>
    /// Date when inventory valuation was completed.
    /// </summary>
    public DateTime? InventoryValuationDate { get; set; }

    /// <summary>
    /// Flag indicating if all accruals and deferrals have been posted.
    /// Ensures proper matching of revenues and expenses.
    /// </summary>
    public bool AccrualsComplete { get; set; } = false;

    /// <summary>
    /// Date when accruals/deferrals were completed.
    /// </summary>
    public DateTime? AccrualsCompletedDate { get; set; }

    // ========================================================================
    // REOPEN CONTROLS AND AUDIT
    // ========================================================================
    
    /// <summary>
    /// Flag indicating if period has ever been reopened after close.
    /// Tracks if period close was reversed for adjustments.
    /// </summary>
    public bool HasBeenReopened { get; set; } = false;

    /// <summary>
    /// Count of times period has been reopened.
    /// High reopen count indicates poor closing procedures or data quality issues.
    /// </summary>
    public int ReopenCount { get; set; } = 0;

    /// <summary>
    /// Date when period was last reopened.
    /// </summary>
    public DateTime? LastReopenedDate { get; set; }

    /// <summary>
    /// User who last reopened the period.
    /// </summary>
    public Guid? LastReopenedByUserId { get; set; }

    /// <summary>
    /// Reason for reopening the period.
    /// Mandatory when period is reopened. Examples: "Audit adjustment", "Correction of material error"
    /// </summary>
    [MaxLength(1000)]
    public string? ReopenReason { get; set; }

    // ========================================================================
    // YEAR-END SPECIFIC (For last period of fiscal year)
    // ========================================================================
    
    /// <summary>
    /// Flag indicating if this is the final period of the fiscal year.
    /// Triggers year-end close procedures (retained earnings transfer, etc.)
    /// </summary>
    public bool IsYearEnd { get; set; } = false;

    /// <summary>
    /// Flag indicating if year-end close procedures have been completed.
    /// Includes retained earnings transfer, closing revenue/expense accounts.
    /// </summary>
    public bool YearEndCloseComplete { get; set; } = false;

    /// <summary>
    /// Date when year-end close was completed.
    /// </summary>
    public DateTime? YearEndCloseDate { get; set; }

    /// <summary>
    /// User who completed year-end close.
    /// </summary>
    public Guid? YearEndClosedByUserId { get; set; }

    // ========================================================================
    // STATISTICS AND METADATA
    // ========================================================================
    
    /// <summary>
    /// Total number of journal entries posted in this period.
    /// Calculated and cached for performance.
    /// </summary>
    public int TotalJournalEntries { get; set; } = 0;

    /// <summary>
    /// Total number of transaction lines posted in this period.
    /// Calculated and cached for performance.
    /// </summary>
    public int TotalTransactionLines { get; set; } = 0;

    /// <summary>
    /// Total debit amount for all transactions in period (base currency).
    /// Cached for quick trial balance generation.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalDebits { get; set; } = 0;

    /// <summary>
    /// Total credit amount for all transactions in period (base currency).
    /// Cached for quick trial balance generation.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalCredits { get; set; } = 0;

    /// <summary>
    /// Calculated difference between debits and credits.
    /// Should always be 0.00 for balanced period.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal BalanceDifference { get; set; } = 0;

    // ========================================================================
    // ADDITIONAL NOTES AND CONTROLS
    // ========================================================================
    
    /// <summary>
    /// Period closing notes or comments.
    /// Used for documenting close procedures, issues encountered, adjustments made.
    /// </summary>
    [MaxLength(2000)]
    public string? ClosingNotes { get; set; }

    /// <summary>
    /// Flag indicating if period allows backdating of transactions.
    /// False = transactions cannot be dated earlier than period start.
    /// </summary>
    public bool AllowBackdating { get; set; } = false;

    /// <summary>
    /// Flag indicating if period allows future dating of transactions.
    /// False = transactions cannot be dated later than period end.
    /// </summary>
    public bool AllowFutureDating { get; set; } = false;

    /// <summary>
    /// Maximum transaction amount allowed without special approval.
    /// NULL = no limit. Used for additional controls during close periods.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? MaxTransactionAmount { get; set; }

    // ========================================================================
    // NAVIGATION PROPERTIES
    // ========================================================================
    
    /// <summary>
    /// The fiscal year this period belongs to.
    /// </summary>
    [ForeignKey(nameof(FiscalYearId))]
    public virtual FiscalYear FiscalYear { get; set; } = null!;

    /// <summary>
    /// Collection of journal entries posted to this period.
    /// </summary>
    public virtual ICollection<JournalEntry> JournalEntries { get; set; } = new List<JournalEntry>();

    /// <summary>
    /// Collection of account transactions posted to this period.
    /// </summary>
    public virtual ICollection<AccountTransaction> Transactions { get; set; } = new List<AccountTransaction>();

    /// <summary>
    /// Collection of module-specific locks for this period.
    /// </summary>
    public virtual ICollection<PeriodModuleLock> PeriodModuleLocks { get; set; } = new List<PeriodModuleLock>();
}