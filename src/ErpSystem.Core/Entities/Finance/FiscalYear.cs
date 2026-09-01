using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// Fiscal Year entity for annual financial period management and year-end controls.
/// Parent entity to FiscalPeriod. A tenant has one authoritative, non-overlapping fiscal
/// calendar because posting-period resolution is not currently scoped by accounting book or
/// reporting framework. Secondary reporting calendars require an explicit calendar/book
/// dimension before overlapping date ranges can be supported safely.
/// 
/// PATTERN: Fiscal Year Management Workflow
/// =========================================
/// - Fiscal years are created before operations begin (typically 2-3 years in advance)
/// - Each fiscal year contains 12 monthly periods or 4 quarterly periods
/// - Year-end close is a critical process involving:
///   * Closing all periods within the year
///   * Running final depreciation and accruals
///   * Performing currency revaluation
///   * Closing revenue/expense accounts to retained earnings
///   * Generating opening balances for next fiscal year
/// - Once locked, fiscal year cannot be reopened without CFO/Controller authorization
/// </summary>
public class FiscalYear : BusinessEntity
{
    // ========================================================================
    // CORE FISCAL YEAR IDENTIFICATION
    // ========================================================================
    
    /// <summary>
    /// Fiscal year name/identifier for display and reference.
    /// Examples: "Fiscal Year 2025", "FY2025", "2025 Financial Year"
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string FiscalYearName { get; set; } = string.Empty;

    /// <summary>
    /// Short code for fiscal year (typically 4-digit year).
    /// Examples: "2025", "FY25", "2025-26"
    /// Used in system references, report headers, and period codes.
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string FiscalYearCode { get; set; } = string.Empty;

    /// <summary>
    /// Calendar year this fiscal year primarily falls in.
    /// For calendar year: 2025
    /// For split year (e.g., July 2024 - June 2025): 2025 (ending year)
    /// Used for quick filtering and reporting.
    /// </summary>
    [Required]
    public int Year { get; set; }

    /// <summary>
    /// Type of fiscal year structure.
    /// Values: "Calendar" (Jan-Dec), "Custom" (any 12-month period), 
    /// "52-53 Week" (retail calendar), "Quarterly" (4 quarters)
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string FiscalYearType { get; set; } = "Calendar";

    // ========================================================================
    // FISCAL YEAR DATE RANGE
    // ========================================================================
    
    /// <summary>
    /// First day of the fiscal year.
    /// Calendar year: January 1
    /// Custom: Any date (e.g., July 1 for government fiscal years)
    /// All periods within this fiscal year must fall within StartDate to EndDate range.
    /// </summary>
    [Required]
    public DateTime StartDate { get; set; }

    /// <summary>
    /// Last day of the fiscal year.
    /// Calendar year: December 31
    /// Custom: 12 months after StartDate (e.g., June 30)
    /// </summary>
    [Required]
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Number of days in this fiscal year.
    /// Typically 365 or 366 (leap year).
    /// For 52-53 week years: 364 or 371 days.
    /// Calculated: (EndDate - StartDate).Days + 1
    /// </summary>
    [Required]
    public int TotalDays { get; set; }

    /// <summary>
    /// Number of periods in this fiscal year.
    /// Monthly: 12 periods
    /// Quarterly: 4 periods  
    /// 52-53 Week: 52 or 53 periods
    /// Used for validation and period generation.
    /// </summary>
    [Required]
    [Range(1, 366)]
    public int NumberOfPeriods { get; set; } = 12;

    // ========================================================================
    // STATUS AND CONTROLS
    // ========================================================================
    
    /// <summary>
    /// Current status of the fiscal year.
    /// Values: "Future", "Open", "Closed", "Locked", "Archived"
    /// 
    /// Status Definitions:
    /// - Future: Year not yet started, setup phase, no transactions allowed
    /// - Open: Active year, transactions can be posted to open periods
    /// - Closed: Year-end close completed, can be reopened by authorized users
    /// - Locked: Permanently locked after audit/external reporting, requires CFO to reopen
    /// - Archived: Historical year archived for long-term retention, read-only
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Future";

    /// <summary>
    /// Indicates if fiscal year is currently active for transaction posting.
    /// TRUE: At least one period is open, transactions can be posted
    /// FALSE: All periods closed or year not yet started
    /// </summary>
    [Required]
    public bool IsActive { get; set; } = false;

    /// <summary>
    /// Indicates if fiscal year is permanently locked.
    /// TRUE: Highest level of protection, only CFO/Controller can unlock
    /// FALSE: Can be closed and reopened by Finance Manager
    /// 
    /// Typically locked after:
    /// - External audit completion
    /// - Annual report publication
    /// - Regulatory filing submission
    /// </summary>
    public bool IsLocked { get; set; } = false;

    /// <summary>
    /// Date when fiscal year was locked.
    /// NULL if never locked.
    /// </summary>
    public DateTime? LockedDate { get; set; }

    /// <summary>
    /// User who locked the fiscal year.
    /// Captured for audit trail and accountability.
    /// </summary>
    public Guid? LockedByUserId { get; set; }

    /// <summary>
    /// Reason for locking the fiscal year.
    /// Examples:
    /// - "Year-end audit completed - auditors signed off"
    /// - "Annual report published and filed with regulators"
    /// - "Tax returns filed and accepted"
    /// </summary>
    [MaxLength(500)]
    public string? LockReason { get; set; }

    // ========================================================================
    // YEAR-END CLOSE WORKFLOW (Section 3.2.X.9)
    // ========================================================================
    
    /// <summary>
    /// Indicates if year-end close process has been initiated.
    /// Set to TRUE when finance team starts year-end close procedures.
    /// Prevents opening of new periods until close is complete or cancelled.
    /// </summary>
    public bool IsCloseInitiated { get; set; } = false;

    /// <summary>
    /// Date when year-end close was initiated.
    /// NULL if close never initiated.
    /// </summary>
    public DateTime? CloseInitiatedDate { get; set; }

    /// <summary>
    /// User who initiated the year-end close process.
    /// </summary>
    public Guid? CloseInitiatedByUserId { get; set; }

    /// <summary>
    /// Indicates if fiscal year has been successfully closed.
    /// TRUE when all year-end procedures completed and validated:
    /// - All periods closed
    /// - Final depreciation run
    /// - Currency revaluation completed
    /// - Revenue/expense accounts closed to retained earnings
    /// - Opening balances generated for next year
    /// </summary>
    public bool IsClosed { get; set; } = false;

    /// <summary>
    /// Date when fiscal year was officially closed.
    /// NULL if not yet closed.
    /// </summary>
    public DateTime? ClosedDate { get; set; }

    /// <summary>
    /// User who closed the fiscal year.
    /// </summary>
    public Guid? ClosedByUserId { get; set; }

    // ========================================================================
    // YEAR-END CLOSE CHECKLIST VALIDATION
    // ========================================================================
    
    /// <summary>
    /// Indicates if all fiscal periods within this year have been closed.
    /// Year cannot be closed until all periods are closed.
    /// </summary>
    public bool AllPeriodsClosedValidated { get; set; } = false;

    /// <summary>
    /// Date when all periods closure was validated.
    /// </summary>
    public DateTime? AllPeriodsClosedValidatedDate { get; set; }

    /// <summary>
    /// Indicates if final depreciation has been run for all fixed assets.
    /// Ensures depreciation is complete for the fiscal year.
    /// </summary>
    public bool FinalDepreciationComplete { get; set; } = false;

    /// <summary>
    /// Date when final depreciation was completed.
    /// </summary>
    public DateTime? FinalDepreciationDate { get; set; }

    /// <summary>
    /// Indicates if year-end currency revaluation has been performed.
    /// Per IAS 21, all foreign currency balances revalued at year-end rates.
    /// </summary>
    public bool YearEndRevaluationComplete { get; set; } = false;

    /// <summary>
    /// Date when year-end revaluation was performed.
    /// </summary>
    public DateTime? YearEndRevaluationDate { get; set; }

    /// <summary>
    /// Indicates if year-end inventory valuation has been completed.
    /// Final inventory counts and valuations verified.
    /// </summary>
    public bool YearEndInventoryComplete { get; set; } = false;

    /// <summary>
    /// Date when year-end inventory was completed.
    /// </summary>
    public DateTime? YearEndInventoryDate { get; set; }

    /// <summary>
    /// Indicates if all year-end accruals and adjustments have been posted.
    /// Includes accrued expenses, deferred revenue, prepayments, etc.
    /// </summary>
    public bool YearEndAccrualsComplete { get; set; } = false;

    /// <summary>
    /// Date when year-end accruals were completed.
    /// </summary>
    public DateTime? YearEndAccrualsDate { get; set; }

    /// <summary>
    /// Indicates if year-end trial balance has been validated.
    /// Ensures debits = credits for entire fiscal year.
    /// </summary>
    public bool YearEndTrialBalanceValidated { get; set; } = false;

    /// <summary>
    /// Date when year-end trial balance validation was performed.
    /// </summary>
    public DateTime? YearEndTrialBalanceDate { get; set; }

    // ========================================================================
    // RETAINED EARNINGS TRANSFER (Year-End Close)
    // ========================================================================
    
    /// <summary>
    /// Indicates if revenue and expense accounts have been closed to retained earnings.
    /// 
    /// Year-End Close Process:
    /// 1. Calculate net income: Total Revenue - Total Expenses
    /// 2. Create closing entry:
    ///    - DEBIT all Revenue accounts (zero them out)
    ///    - CREDIT all Expense accounts (zero them out)
    ///    - If net income: CREDIT Retained Earnings
    ///    - If net loss: DEBIT Retained Earnings
    /// 3. Revenue/Expense accounts start next year with zero balance
    /// </summary>
    public bool RetainedEarningsTransferComplete { get; set; } = false;

    /// <summary>
    /// Date when retained earnings transfer was completed.
    /// </summary>
    public DateTime? RetainedEarningsTransferDate { get; set; }

    /// <summary>
    /// Reference to the closing journal entry that transferred to retained earnings.
    /// Links to JournalEntry record for audit trail.
    /// </summary>
    public Guid? ClosingJournalEntryId { get; set; }

    /// <summary>
    /// Net income (or loss) for the fiscal year transferred to retained earnings.
    /// Positive = Net Income (profitable year)
    /// Negative = Net Loss (unprofitable year)
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? NetIncomeTransferred { get; set; }

    // ========================================================================
    // OPENING BALANCES FOR NEXT YEAR
    // ========================================================================
    
    /// <summary>
    /// Indicates if opening balances for next fiscal year have been generated.
    /// 
    /// Process:
    /// - Balance sheet accounts (Assets, Liabilities, Equity): Carry forward closing balance
    /// - Income statement accounts (Revenue, Expense): Start at zero
    /// - Opening balances posted to first period of next fiscal year
    /// </summary>
    public bool OpeningBalancesGenerated { get; set; } = false;

    /// <summary>
    /// Date when opening balances were generated.
    /// </summary>
    public DateTime? OpeningBalancesGeneratedDate { get; set; }

    /// <summary>
    /// Reference to the next fiscal year (if opening balances created).
    /// Links forward for year-over-year navigation.
    /// </summary>
    public Guid? NextFiscalYearId { get; set; }

    /// <summary>
    /// Reference to the opening balance journal entry in next fiscal year.
    /// Links to JournalEntry record for audit trail.
    /// </summary>
    public Guid? OpeningBalanceJournalEntryId { get; set; }

    // ========================================================================
    // REOPEN CONTROLS AND AUDIT
    // ========================================================================
    
    /// <summary>
    /// Indicates if fiscal year has been reopened after initial close.
    /// TRUE: Year was closed and subsequently reopened for adjustments
    /// FALSE: Year has never been reopened (or never closed)
    /// </summary>
    public bool HasBeenReopened { get; set; } = false;

    /// <summary>
    /// Count of times fiscal year has been reopened.
    /// High count indicates control weaknesses or data quality issues.
    /// Typically should be 0 or 1 maximum.
    /// </summary>
    public int ReopenCount { get; set; } = 0;

    /// <summary>
    /// Date when fiscal year was last reopened.
    /// NULL if never reopened.
    /// </summary>
    public DateTime? LastReopenedDate { get; set; }

    /// <summary>
    /// User who last reopened the fiscal year.
    /// </summary>
    public Guid? LastReopenedByUserId { get; set; }

    /// <summary>
    /// Reason for reopening the fiscal year.
    /// Mandatory when fiscal year is reopened.
    /// 
    /// Examples:
    /// - "Audit adjustment required - understated revenue by 50,000 GHS"
    /// - "Correction of material error in depreciation calculation"
    /// - "Prior period adjustment per IAS 8"
    /// </summary>
    [MaxLength(1000)]
    public string? ReopenReason { get; set; }

    // ========================================================================
    // CLASSIFICATION AND REPORTING FRAMEWORK
    // ========================================================================
    
    /// <summary>
    /// Primary reporting framework for this fiscal year.
    /// Values: "IFRS", "US GAAP", "Local GAAP", "Tax Basis"
    /// 
    /// Organizations may maintain multiple fiscal years for different frameworks:
    /// - FY2025-IFRS: January 1 - December 31 (IFRS reporting)
    /// - FY2025-TAX: July 1 - June 30 (Ghana tax year)
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string ReportingFramework { get; set; } = "IFRS";

    /// <summary>
    /// Currency code for this fiscal year (base currency for all transactions).
    /// Typically organization's home currency (e.g., "GHS" for Ghana Cedis).
    /// ISO 4217 three-letter code.
    /// </summary>
    [Required]
    [MaxLength(3)]
    public string BaseCurrency { get; set; } = "GHS";

    // ========================================================================
    // STATISTICS AND SUMMARY DATA (Cached for Performance)
    // ========================================================================
    
    /// <summary>
    /// Total number of journal entries posted within this fiscal year.
    /// Calculated and cached for dashboard reporting.
    /// </summary>
    public int TotalJournalEntries { get; set; } = 0;

    /// <summary>
    /// Total number of transaction lines posted within this fiscal year.
    /// Calculated and cached for volume analysis.
    /// </summary>
    public int TotalTransactionLines { get; set; } = 0;

    /// <summary>
    /// Total debit amount for all transactions in this fiscal year (base currency).
    /// Cached for quick financial statement generation.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalDebits { get; set; } = 0;

    /// <summary>
    /// Total credit amount for all transactions in this fiscal year (base currency).
    /// Cached for quick financial statement generation.
    /// Should always equal TotalDebits (balanced books).
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalCredits { get; set; } = 0;

    /// <summary>
    /// Calculated difference between total debits and total credits.
    /// Should always be 0.00 for balanced books.
    /// Non-zero indicates out-of-balance condition requiring investigation.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal BalanceDifference { get; set; } = 0;

    /// <summary>
    /// Total revenue recognized during this fiscal year.
    /// Sum of all credit balances in revenue accounts.
    /// Cached for quick P&amp;L generation.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalRevenue { get; set; } = 0;

    /// <summary>
    /// Total expenses incurred during this fiscal year.
    /// Sum of all debit balances in expense accounts.
    /// Cached for quick P&amp;L generation.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalExpenses { get; set; } = 0;

    /// <summary>
    /// Net income for the fiscal year (calculated).
    /// NetIncome = TotalRevenue - TotalExpenses
    /// Positive = Profitable year, Negative = Loss year
    /// Transferred to retained earnings during year-end close.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal NetIncome { get; set; } = 0;

    // ========================================================================
    // BUDGET AND PLANNING
    // ========================================================================
    
    /// <summary>
    /// Indicates if budget has been approved for this fiscal year.
    /// TRUE: Budget approved, available for budget vs actual reporting
    /// FALSE: Budget not yet approved or not prepared
    /// </summary>
    public bool IsBudgetApproved { get; set; } = false;

    /// <summary>
    /// Date when budget was approved.
    /// NULL if budget not yet approved.
    /// </summary>
    public DateTime? BudgetApprovedDate { get; set; }

    /// <summary>
    /// User who approved the budget.
    /// Typically CFO or Board of Directors.
    /// </summary>
    public Guid? BudgetApprovedByUserId { get; set; }

    /// <summary>
    /// Total budgeted revenue for this fiscal year.
    /// Used for budget vs actual variance analysis.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? BudgetedRevenue { get; set; }

    /// <summary>
    /// Total budgeted expenses for this fiscal year.
    /// Used for budget vs actual variance analysis.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? BudgetedExpenses { get; set; }

    /// <summary>
    /// Budgeted net income for this fiscal year.
    /// BudgetedRevenue - BudgetedExpenses
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? BudgetedNetIncome { get; set; }

    // ========================================================================
    // AUDIT AND EXTERNAL REPORTING
    // ========================================================================
    
    /// <summary>
    /// Indicates if external audit has been completed for this fiscal year.
    /// TRUE: Auditors have issued their opinion
    /// FALSE: Audit not yet completed or not required
    /// </summary>
    public bool IsAuditComplete { get; set; } = false;

    /// <summary>
    /// Date when external audit was completed.
    /// </summary>
    public DateTime? AuditCompletedDate { get; set; }

    /// <summary>
    /// Name of external audit firm.
    /// Example: "KPMG Ghana", "PwC Accra", "Deloitte"
    /// </summary>
    [MaxLength(200)]
    public string? AuditFirm { get; set; }

    /// <summary>
    /// Audit opinion issued by external auditors.
    /// Values: "Unqualified" (clean), "Qualified", "Adverse", "Disclaimer"
    /// </summary>
    [MaxLength(50)]
    public string? AuditOpinion { get; set; }

    /// <summary>
    /// Reference number for audit report.
    /// </summary>
    [MaxLength(100)]
    public string? AuditReportReference { get; set; }

    // ========================================================================
    // NOTES AND COMMENTS
    // ========================================================================
    
    /// <summary>
    /// General notes or comments about this fiscal year.
    /// Examples:
    /// - "Fiscal year extended by 1 month due to business restructuring"
    /// - "First year under IFRS 16 lease accounting standard"
    /// - "Year impacted by COVID-19 pandemic"
    /// </summary>
    [MaxLength(2000)]
    public string? Notes { get; set; }

    /// <summary>
    /// Year-end closing notes documenting the close process.
    /// Captures issues encountered, adjustments made, approval confirmations.
    /// </summary>
    [MaxLength(2000)]
    public string? YearEndClosingNotes { get; set; }

    // ========================================================================
    // NAVIGATION PROPERTIES
    // ========================================================================
    
    /// <summary>
    /// Collection of fiscal periods within this fiscal year.
    /// Typically 12 monthly periods or 4 quarterly periods.
    /// </summary>
    public virtual ICollection<FiscalPeriod> FiscalPeriods { get; set; } = new List<FiscalPeriod>();

    /// <summary>
    /// Reference to the next fiscal year (if opening balances were generated).
    /// Enables year-over-year navigation and reporting.
    /// </summary>
    [ForeignKey(nameof(NextFiscalYearId))]
    public virtual FiscalYear? NextFiscalYear { get; set; }

    /// <summary>
    /// Reference to the previous fiscal year (if this year received opening balances).
    /// Enables backward navigation for comparative reporting.
    /// </summary>
    public virtual FiscalYear? PreviousFiscalYear { get; set; }

    /// <summary>
    /// Reference to the closing journal entry (retained earnings transfer).
    /// Links to the year-end closing entry for audit trail.
    /// </summary>
    [ForeignKey(nameof(ClosingJournalEntryId))]
    public virtual JournalEntry? ClosingJournalEntry { get; set; }

    /// <summary>
    /// Reference to the opening balance journal entry in next fiscal year.
    /// Links to the opening balances for audit trail.
    /// </summary>
    [ForeignKey(nameof(OpeningBalanceJournalEntryId))]
    public virtual JournalEntry? OpeningBalanceJournalEntry { get; set; }
}
