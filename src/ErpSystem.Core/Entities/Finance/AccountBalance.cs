using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// Optional optimization table for fast balance queries without aggregating AccountTransaction records.
/// Stores pre-calculated functional-currency account balances by fiscal period and accounting book.
/// Updated automatically when transactions are posted to maintain real-time accuracy.
/// </summary>
/// <remarks>
/// Performance Benefits:
/// - Eliminates need to sum thousands of AccountTransaction records for balance inquiries
/// - Enables instant Trial Balance and Financial Statement generation
/// - Supports exact-book functional-currency reporting; transaction-currency exposure is separate
/// - Provides year-to-date totals without complex aggregation queries
/// 
/// Update Strategy:
/// - Real-time updates on transaction posting (immediate consistency)
/// - Cached balances invalidated via LastUpdated timestamp
/// - Periodic reconciliation with AccountTransaction table for data integrity
/// </remarks>
public class AccountBalance : BusinessEntity
{
    // ========================================================================
    // CORE IDENTIFICATION AND LINKING
    // ========================================================================
    
    /// <summary>
    /// Reference to the GL Account for which this balance is maintained.
    /// Links to Account with full segment structure.
    /// </summary>
    [Required]
    public Guid AccountId { get; set; }

    /// <summary>
    /// Reference to the fiscal period for this balance snapshot.
    /// Separate balance records maintained for each period.
    /// </summary>
    [Required]
    public Guid FiscalPeriodId { get; set; }

    /// <summary>
    /// Stable tenant-owned accounting-book identity. Journal lines remain authoritative; this row is
    /// only a rebuildable read model for the exact book.
    /// </summary>
    [Required]
    public Guid AccountingBookId { get; set; }

    /// <summary>
    /// Immutable stable-code snapshot of the exact accounting book represented by this row.
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string BookClassification { get; set; } = "IFRS";

    /// <summary>
    /// Tenant functional currency for this balance row. Foreign-currency amounts are held only in
    /// AccountCurrencyExposure and must not create additional AccountBalance currency rows.
    /// </summary>
    [MaxLength(3)]
    public string Currency { get; set; } = string.Empty;

    // ========================================================================
    // BALANCE AMOUNTS (PERIOD-SPECIFIC)
    // ========================================================================
    
    /// <summary>
    /// Opening balance at the start of the fiscal period.
    /// Represents closing balance from previous period.
    /// For first period of fiscal year, represents brought-forward balance.
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal OpeningBalance { get; set; } = 0;

    /// <summary>
    /// Opening balance type indicator (Debit or Credit).
    /// Values: "DR" (Debit), "CR" (Credit)
    /// Determines how opening balance affects current period calculations.
    /// </summary>
    [Required]
    [MaxLength(2)]
    public string OpeningBalanceType { get; set; } = "DR";

    /// <summary>
    /// Total debit amount posted during the fiscal period.
    /// Sum of all debit transactions for this account in current period.
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal PeriodDebits { get; set; } = 0;

    /// <summary>
    /// Total credit amount posted during the fiscal period.
    /// Sum of all credit transactions for this account in current period.
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal PeriodCredits { get; set; } = 0;

    /// <summary>
    /// Net movement during the period (Debits - Credits).
    /// Positive value indicates net debit movement, negative indicates net credit.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal PeriodNetMovement { get; set; } = 0;

    /// <summary>
    /// Closing balance at the end of the fiscal period.
    /// Calculated in the invariant signed coordinate: Opening + Debits - Credits.
    /// Becomes opening balance for next period.
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal ClosingBalance { get; set; } = 0;

    /// <summary>
    /// Closing balance type indicator (Debit or Credit).
    /// Values: "DR" (Debit), "CR" (Credit)
    /// Indicates whether closing balance is debit or credit nature.
    /// </summary>
    [Required]
    [MaxLength(2)]
    public string ClosingBalanceType { get; set; } = "DR";

    // ========================================================================
    // YEAR-TO-DATE TOTALS (FISCAL YEAR ACCUMULATION)
    // ========================================================================
    
    /// <summary>
    /// Year-to-date total debits (from start of fiscal year to end of current period).
    /// Cumulative sum of all debit transactions from fiscal year start.
    /// Used for annual financial statement preparation.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal YearToDateDebits { get; set; } = 0;

    /// <summary>
    /// Year-to-date total credits (from start of fiscal year to end of current period).
    /// Cumulative sum of all credit transactions from fiscal year start.
    /// Used for annual financial statement preparation.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal YearToDateCredits { get; set; } = 0;

    /// <summary>
    /// Year-to-date net movement (YTD Debits - YTD Credits).
    /// Represents cumulative impact on account balance for fiscal year.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal YearToDateNetMovement { get; set; } = 0;

    // ========================================================================
    // SEGMENT DIMENSION TRACKING (FOR REPORTING)
    // ========================================================================
    
    /// <summary>
    /// Cached segment string for performance and reporting.
    /// Example: "001-FIN-1000-01" representing Company-Department-Account-SubAccount.
    /// Populated from Account's segment values for fast segment-based filtering.
    /// Updated when account segment structure changes.
    /// </summary>
    [MaxLength(200)]
    public string? SegmentString { get; set; }

    /// <summary>
    /// Department segment value for reporting dimension filtering.
    /// Extracted from account's segment structure if flagged as reporting dimension.
    /// NULL if Department is not a reporting dimension.
    /// </summary>
    [MaxLength(20)]
    public string? DepartmentSegment { get; set; }

    /// <summary>
    /// Cost Center segment value for reporting dimension filtering.
    /// Extracted from account's segment structure if flagged as reporting dimension.
    /// NULL if Cost Center is not a reporting dimension.
    /// </summary>
    [MaxLength(20)]
    public string? CostCenterSegment { get; set; }

    /// <summary>
    /// Project segment value for reporting dimension filtering.
    /// Extracted from account's segment structure if flagged as reporting dimension.
    /// NULL if Project is not a reporting dimension or account not project-specific.
    /// </summary>
    [MaxLength(20)]
    public string? ProjectSegment { get; set; }

    /// <summary>
    /// Location segment value for reporting dimension filtering.
    /// Extracted from account's segment structure if flagged as reporting dimension.
    /// NULL if Location is not a reporting dimension.
    /// </summary>
    [MaxLength(20)]
    public string? LocationSegment { get; set; }

    // ========================================================================
    // LEGACY FX FIELDS (NOT AUTHORITATIVE IN THE C2 BALANCE GRAIN)
    // ========================================================================
    
    /// <summary>
    /// Retained predecessor-schema field. C2 never uses it to represent transaction-currency
    /// exposure; AccountCurrencyExposure is the exact-book authority for that evidence.
    /// </summary>
    [Column(TypeName = "decimal(18,6)")]
    public decimal? ExchangeRate { get; set; }

    /// <summary>
    /// Retained predecessor-schema field. AccountBalance is already in the tenant functional
    /// currency, so new C2 projection code does not derive or consume this value.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? BaseCurrencyEquivalent { get; set; }

    /// <summary>
    /// Retained predecessor-schema field. Revaluation and exact-book exposure evidence live in
    /// their dedicated models and must not be inferred from this nullable compatibility value.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? UnrealizedGainLoss { get; set; }

    // ========================================================================
    // TRANSACTION STATISTICS (FOR PERFORMANCE AND MONITORING)
    // ========================================================================
    
    /// <summary>
    /// Number of transactions posted to this account during the period.
    /// Includes both debit and credit transactions.
    /// Used for transaction volume analysis and performance monitoring.
    /// </summary>
    public int TransactionCount { get; set; } = 0;

    /// <summary>
    /// Timestamp of last transaction posted to this account balance.
    /// Used for cache invalidation and balance recalculation triggers.
    /// NULL if no transactions posted in current period.
    /// </summary>
    public DateTime? LastTransactionDate { get; set; }

    /// <summary>
    /// User ID who posted the last transaction affecting this balance.
    /// Provides audit trail for balance changes.
    /// NULL if no transactions posted in current period.
    /// </summary>
    public Guid? LastTransactionUserId { get; set; }

    // ========================================================================
    // CACHE CONTROL AND RECONCILIATION
    // ========================================================================
    
    /// <summary>
    /// Timestamp when this balance record was last updated/calculated.
    /// Used for cache invalidation and staleness detection.
    /// Updated on every transaction posting or balance recalculation.
    /// </summary>
    [Required]
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Flag indicating whether this balance has been reconciled with AccountTransaction table.
    /// True = Balance matches sum of transactions (verified)
    /// False = Balance needs reconciliation or has discrepancy
    /// </summary>
    public bool IsReconciled { get; set; } = true;

    /// <summary>
    /// Date when balance was last reconciled with transaction details.
    /// System performs periodic reconciliation (daily/weekly) to ensure data integrity.
    /// NULL if never reconciled.
    /// </summary>
    public DateTime? LastReconciledDate { get; set; }

    /// <summary>
    /// Discrepancy amount if reconciliation found variance.
    /// NULL if IsReconciled = true or reconciliation not performed.
    /// Non-zero value triggers investigation and correction workflow.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? ReconciliationDiscrepancy { get; set; }

    // ========================================================================
    // PERIOD CLOSE AND FINALIZATION
    // ========================================================================
    
    /// <summary>
    /// Flag indicating whether this period balance is locked/finalized.
    /// True when fiscal period is closed and balance should not change.
    /// Prevents accidental modifications to closed period balances.
    /// </summary>
    public bool IsLocked { get; set; } = false;

    /// <summary>
    /// Date when balance was locked (period close date).
    /// NULL if period still open or balance not locked.
    /// </summary>
    public DateTime? LockedDate { get; set; }

    /// <summary>
    /// User who locked this balance record (typically during period close).
    /// NULL if not locked.
    /// </summary>
    public Guid? LockedByUserId { get; set; }

    // ========================================================================
    // REPORTING AND ANALYSIS FLAGS
    // ========================================================================
    
    /// <summary>
    /// Flag indicating if account had any activity during the period.
    /// False = Opening balance = Closing balance and no transactions.
    /// Used to filter dormant accounts from reports.
    /// </summary>
    public bool HasActivity { get; set; } = false;

    /// <summary>
    /// Flag indicating if closing balance is zero.
    /// Used for report filtering and account cleanup processes.
    /// </summary>
    public bool IsZeroBalance { get; set; } = false;

    /// <summary>
    /// Flag indicating if account balance is negative (unusual for most account types).
    /// Asset/Expense accounts typically have debit balances.
    /// Liability/Equity/Revenue accounts typically have credit balances.
    /// Negative balance may indicate data entry error or policy violation.
    /// </summary>
    public bool IsNegativeBalance { get; set; } = false;

    /// <summary>
    /// Optional notes or comments regarding balance calculations or adjustments.
    /// Used for documenting unusual balance situations or manual corrections.
    /// </summary>
    [MaxLength(1000)]
    public string? Notes { get; set; }

    // ========================================================================
    // NAVIGATION PROPERTIES
    // ========================================================================
    
    /// <summary>
    /// The GL Account for which this balance is maintained.
    /// Provides access to account details, segment structure, and configuration.
    /// </summary>
    [ForeignKey(nameof(AccountId))]
    public virtual Account Account { get; set; } = null!;

    /// <summary>
    /// The fiscal period for this balance snapshot.
    /// Links to period definition, dates, and status information.
    /// </summary>
    [ForeignKey(nameof(FiscalPeriodId))]
    public virtual FiscalPeriod FiscalPeriod { get; set; } = null!;

    [ForeignKey(nameof(AccountingBookId))]
    public virtual AccountingBook AccountingBook { get; set; } = null!;

    // ========================================================================
    // COMPUTED PROPERTIES (NOT MAPPED TO DATABASE)
    // ========================================================================
    
    /// <summary>
    /// Computed property: Returns true if balance is on the natural side for account type.
    /// Asset/Expense accounts: Natural debit balance
    /// Liability/Equity/Revenue accounts: Natural credit balance
    /// Used for report formatting and balance validation.
    /// </summary>
    [NotMapped]
    public bool IsNaturalBalance
    {
        get
        {
            // This would be calculated based on Account.AccountType
            // and comparing with ClosingBalanceType
            // Implementation requires Account navigation property to be loaded
            return true; // Placeholder - actual logic would check account type
        }
    }

    /// <summary>
    /// Computed property: Absolute value of closing balance.
    /// Used for reporting where sign is indicated separately.
    /// </summary>
    [NotMapped]
    public decimal ClosingBalanceAbsolute => Math.Abs(ClosingBalance);

    /// <summary>
    /// Computed property: Indicates if balance record is stale (needs refresh).
    /// Stale if LastUpdated is older than configured threshold (e.g., 1 hour).
    /// </summary>
    [NotMapped]
    public bool IsStale
    {
        get
        {
            var threshold = TimeSpan.FromHours(1);
            return DateTime.UtcNow - LastUpdated > threshold;
        }
    }
}

/// <summary>
/// Index recommendations for AccountBalance table to optimize query performance.
/// 
/// CRITICAL INDEXES:
/// 1. Composite index on (TenantId, AccountId, AccountingBookId, FiscalPeriodId, Currency)
///    - Primary lookup pattern for balance inquiries
///    - Ensures unique constraint on balance records
/// 
/// 2. Index on (TenantId, AccountingBookId, FiscalPeriodId)
///    - Used for Trial Balance generation (all accounts for a period)
///    - Supports period-level aggregation queries
/// 
/// 3. Index on (TenantId, AccountId, AccountingBookId) INCLUDE (ClosingBalance, Currency)
///    - Multi-period balance history queries
///    - Account roll-forward reports
/// 
/// 4. Index on (DepartmentSegment, FiscalPeriodId) WHERE DepartmentSegment IS NOT NULL
///    - Departmental reporting queries
///    - Filtered index for reporting dimensions only
/// 
/// 5. Index on (LastUpdated) WHERE IsReconciled = 0
///    - Identifies stale balances needing reconciliation
///    - Filtered index for maintenance processes
/// 
/// 6. Index on (FiscalPeriodId, IsLocked, HasActivity)
///    - Period close validation queries
///    - Activity analysis and dormant account identification
/// </summary>
/// <remarks>
/// Database constraints to implement:
/// 
/// UNIQUE CONSTRAINT:
/// - (TenantId, AccountId, AccountingBookId, FiscalPeriodId, Currency)
///   Ensures only one balance record per account-period-book-currency combination
/// 
/// CHECK CONSTRAINTS:
/// - OpeningBalanceType IN ('DR', 'CR')
/// - ClosingBalanceType IN ('DR', 'CR')
/// - BookClassification is an immutable snapshot matching the related AccountingBook.Code
/// - PeriodDebits >= 0
/// - PeriodCredits >= 0
/// - TransactionCount >= 0
/// 
/// FOREIGN KEY CONSTRAINTS:
/// - AccountId references Account(AccountId)
/// - FiscalPeriodId references FiscalPeriod(FiscalPeriodId)
/// - Both with ON DELETE RESTRICT to prevent orphaned balance records
/// </remarks>
