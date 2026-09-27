using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;

namespace ErpSystem.Core.Entities.Finance
{
    /// <summary>
    /// Stores exchange rates for currency conversion and revaluation.
    /// 
    /// PATTERN: Exchange Rate Management Workflow
    /// ==========================================
    /// System maintains historical exchange rates from multiple sources:
    /// - Automatic daily updates from external APIs (ExchangeRatesAPI.io, XE Currency Data)
    /// - Manual entry by finance team when needed
    /// - Budget rates for planning purposes
    /// - Average rates for periodic calculations
    /// 
    /// Rate Types Supported:
    /// - Daily (spot rates for transaction date)
    /// - Average (monthly/quarterly averages)
    /// - Month-End (period-end rates for IAS 21 revaluation)
    /// - Year-End (fiscal year-end rates)
    /// - Budget (planning rates)
    /// - Fixed (predetermined rates for special scenarios)
    /// 
    /// Historical Integrity:
    /// Once a rate is used in transactions, it becomes part of the audit trail
    /// and cannot be deleted (only marked inactive or superseded by new rates).
    /// </summary>
    public class ExchangeRate : BusinessEntity
    {
        #region Core Rate Properties

        /// <summary>
        /// Base currency code - the currency being converted FROM.
        /// Typically the organization's home currency (e.g., "GHS" for Ghana Cedis).
        /// ISO 4217 three-letter code.
        /// </summary>
        [Required]
        [MaxLength(3)]
        public string BaseCurrencyCode { get; set; } = "GHS";

        /// <summary>
        /// Target currency code - the currency being converted TO.
        /// Examples: "USD", "EUR", "GBP", "NGN"
        /// ISO 4217 three-letter code.
        /// </summary>
        [Required]
        [MaxLength(3)]
        public string TargetCurrencyCode { get; set; } = string.Empty;

        /// <summary>
        /// Exchange rate value expressed as:
        /// 1 unit of BaseCurrency = ExchangeRate units of TargetCurrency
        /// 
        /// Example 1: 1 USD = 15.25 GHS
        /// - BaseCurrencyCode = "GHS"
        /// - TargetCurrencyCode = "USD"
        /// - ExchangeRate = 0.065574
        /// 
        /// Example 2: 1 EUR = 17.50 GHS
        /// - BaseCurrencyCode = "GHS"
        /// - TargetCurrencyCode = "EUR"
        /// - ExchangeRate = 0.057143
        /// 
        /// Precision: 6 decimal places to handle currencies with small denominations.
        /// </summary>
        [Required]
        [Column(TypeName = "decimal(18,6)")]
        public decimal Rate { get; set; }

        /// <summary>
        /// Inverse rate (calculated automatically) expressed as:
        /// 1 unit of TargetCurrency = InverseRate units of BaseCurrency
        /// 
        /// Example: If Rate = 0.08 (1 GHS = 0.08 USD)
        /// Then InverseRate = 12.5 (1 USD = 12.5 GHS)
        /// 
        /// Calculated as: InverseRate = 1 / Rate
        /// Stored for convenience and performance optimization.
        /// </summary>
        [Column(TypeName = "decimal(18,6)")]
        public decimal InverseRate { get; set; }

        /// <summary>
        /// Effective date of this exchange rate.
        /// Transactions dated on or after this date can use this rate.
        /// 
        /// For daily rates: Transaction date
        /// For month-end rates: Last day of the month
        /// For year-end rates: Last day of fiscal year
        /// </summary>
        [Required]
        public DateTime EffectiveDate { get; set; }

        /// <summary>
        /// End date when this rate ceased to be effective.
        /// NULL indicates the rate is still current/active.
        /// 
        /// When a new rate is entered for the same currency pair and rate type,
        /// the previous rate's EndDate is automatically set.
        /// </summary>
        public DateTime? EndDate { get; set; }

        #endregion

        #region Rate Type and Classification

        /// <summary>
        /// Type of exchange rate for appropriate usage in transactions and reporting.
        /// 
        /// Rate Type Usage:
        /// - Daily: Normal transaction processing (invoice date, payment date)
        /// - Average: Monthly/quarterly average for smoothing volatile currencies
        /// - Month-End: Period-end revaluation per IAS 21 compliance
        /// - Year-End: Fiscal year-end financial statements
        /// - Budget: Budget planning and forecasting
        /// - Fixed: Special contractual arrangements or hedged rates
        /// - Spot: Real-time rates (rarely stored, typically for reference)
        /// </summary>
        [Required]
        public ExchangeRateType RateType { get; set; }

        /// <summary>
        /// Quote side from the rate provider/bank perspective. Buying means the
        /// provider buys the foreign (target) currency; Selling means it sells it.
        /// Mid is the neutral accounting/reference quote.
        /// </summary>
        [Required]
        public ExchangeRateQuoteSide QuoteSide { get; set; } = ExchangeRateQuoteSide.Mid;

        /// <summary>
        /// Indicates if this is the current active rate for its type and currency pair.
        /// 
        /// Only ONE rate can be active per currency pair per rate type at any time.
        /// When a new rate is entered, the previous rate is automatically marked inactive.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Priority/preference for rate selection when multiple rates available.
        /// Higher number = higher priority.
        /// 
        /// Used when multiple rate sources provide rates on the same date.
        /// Example: Central Bank rate might have priority 10, API rate priority 5.
        /// </summary>
        public int Priority { get; set; } = 0;

        #endregion

        #region Source and Provenance

        /// <summary>
        /// Source of the exchange rate for audit and credibility tracking.
        /// 
        /// Common Sources:
        /// - "Bank of Ghana" - Central bank official rates
        /// - "ExchangeRatesAPI" - API service provider
        /// - "XE Currency Data" - Commercial currency data provider
        /// - "Bloomberg" - Financial data provider
        /// - "Reuters" - Financial news and data provider
        /// - "Manual Entry" - Finance team manual input
        /// - "System Default" - Fallback default rates
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string RateSource { get; set; } = "Manual Entry";

        /// <summary>
        /// Indicates if rate was entered manually or retrieved automatically.
        /// 
        /// TRUE: Rate entered by user through the system interface.
        /// FALSE: Rate retrieved automatically from external API or data feed.
        /// 
        /// Manual rates typically have higher priority for compliance reasons.
        /// </summary>
        public bool IsManualEntry { get; set; } = true;

        /// <summary>
        /// API endpoint or URL from which the rate was retrieved (if automatic).
        /// NULL for manual entries.
        /// 
        /// Example: "https://api.exchangeratesapi.io/latest?base=GHS"
        /// Useful for troubleshooting and audit trail.
        /// </summary>
        [MaxLength(500)]
        public string? APIEndpoint { get; set; }

        /// <summary>
        /// Response metadata or reference ID from API provider.
        /// Stored for troubleshooting and data verification.
        /// </summary>
        [MaxLength(1000)]
        public string? APIResponseMetadata { get; set; }

        #endregion

        #region Usage and Statistics

        /// <summary>
        /// Indicates if this rate has been used in any financial transactions.
        /// 
        /// Once TRUE, the rate becomes part of the audit trail and:
        /// - Cannot be deleted (only marked inactive)
        /// - Cannot be modified (rate changes require new records)
        /// - Must be retained per data retention policies
        /// 
        /// Automatically set to TRUE when first transaction uses this rate.
        /// </summary>
        public bool HasBeenUsedInTransactions { get; set; } = false;

        /// <summary>
        /// Count of transactions using this specific exchange rate.
        /// Updated automatically by transaction posting process.
        /// Used for analytics and rate usage reporting.
        /// </summary>
        public int TransactionCount { get; set; } = 0;

        /// <summary>
        /// Date when this rate was first used in a transaction.
        /// NULL if never used.
        /// </summary>
        public DateTime? FirstUsedDate { get; set; }

        /// <summary>
        /// Date when this rate was last used in a transaction.
        /// NULL if never used.
        /// </summary>
        public DateTime? LastUsedDate { get; set; }

        #endregion

        #region Variance and Thresholds

        /// <summary>
        /// Percentage change from previous rate (for the same currency pair and type).
        /// 
        /// Calculation:
        /// RateChangePercentage = ((NewRate - PreviousRate) / PreviousRate) × 100
        /// 
        /// Example:
        /// - Previous Rate: 15.00
        /// - New Rate: 15.75
        /// - RateChangePercentage = ((15.75 - 15.00) / 15.00) × 100 = 5.00%
        /// 
        /// Used for alerting significant rate movements (typically >5% daily change).
        /// </summary>
        [Column(TypeName = "decimal(18,4)")]
        public decimal? RateChangePercentage { get; set; }

        /// <summary>
        /// Absolute change in rate value from previous rate.
        /// 
        /// Example:
        /// - Previous Rate: 15.00
        /// - New Rate: 15.75
        /// - RateChangeAmount = 0.75
        /// 
        /// Stored for quick variance reporting without recalculation.
        /// </summary>
        [Column(TypeName = "decimal(18,6)")]
        public decimal? RateChangeAmount { get; set; }

        /// <summary>
        /// Reference to the previous exchange rate record (for change tracking).
        /// NULL if this is the first rate for this currency pair and type.
        /// </summary>
        public Guid? PreviousRateId { get; set; }

        /// <summary>
        /// Indicates if rate change exceeded configured variance threshold.
        /// 
        /// Triggers alerts when TRUE (typically for >5% daily variance).
        /// Finance team reviews flagged rates for unusual market movements.
        /// </summary>
        public bool ExceedsVarianceThreshold { get; set; } = false;

        #endregion

        #region Validation and Approval

        /// <summary>
        /// Approval status for rates requiring management review.
        /// 
        /// Typically required when:
        /// - Rate variance exceeds threshold
        /// - Manual rates significantly differ from market rates
        /// - High-value transactions will use this rate
        /// </summary>
        public RateApprovalStatus ApprovalStatus { get; set; } = RateApprovalStatus.Approved;

        /// <summary>
        /// User ID who approved this rate (if approval was required).
        /// NULL if auto-approved or no approval workflow configured.
        /// </summary>
        public Guid? ApprovedByUserId { get; set; }

        /// <summary>
        /// Date and time when rate was approved.
        /// NULL if not yet approved or approval not required.
        /// </summary>
        public DateTime? ApprovalDate { get; set; }

        /// <summary>
        /// Comments or notes about the rate or approval decision.
        /// 
        /// Examples:
        /// - "Rate confirmed with Bank of Ghana official bulletin"
        /// - "Significant variance due to market volatility - approved by CFO"
        /// - "Budget rate set for FY2026 planning"
        /// </summary>
        [MaxLength(1000)]
        public string? Comments { get; set; }

        #endregion

        #region Audit and Metadata

        /// <summary>
        /// User ID who created this exchange rate record.
        /// System user ID for API-retrieved rates, actual user for manual entries.
        /// </summary>
        [Required]
        public Guid CreatedByUserId { get; set; }

        /// <summary>
        /// Timestamp when rate record was created in the system.
        /// </summary>
        [Required]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// User ID who last modified this rate record.
        /// NULL if no modifications since creation.
        /// 
        /// NOTE: Rate value itself should NOT be modified after use in transactions.
        /// Modifications typically only to metadata (comments, approval status).
        /// </summary>
        public Guid? ModifiedByUserId { get; set; }

        /// <summary>
        /// Timestamp of last modification.
        /// NULL if no modifications since creation.
        /// </summary>
        public DateTime? ModifiedDate { get; set; }

        #endregion

        #region Navigation Properties

        /// <summary>
        /// Reference to previous rate record for change tracking and trend analysis.
        /// </summary>
        [ForeignKey(nameof(PreviousRateId))]
        public virtual ExchangeRate? PreviousRate { get; set; }

        /// <summary>
        /// Subsequent rate record (if this rate has been superseded).
        /// NULL if this is the most current rate.
        /// </summary>
        public virtual ExchangeRate? NextRate { get; set; }

        #endregion
    }

    /// <summary>
    /// Exchange rate type enumeration for different rate applications.
    /// </summary>
    public enum ExchangeRateType
    {
        /// <summary>
        /// Daily spot rate for normal transaction processing.
        /// Used for invoices, payments, and receipts on transaction date.
        /// </summary>
        Daily = 1,

        /// <summary>
        /// Monthly average rate for smoothing volatile currencies.
        /// Calculated as average of daily rates for the month.
        /// </summary>
        Average = 2,

        /// <summary>
        /// Month-end closing rate for period-end financial reporting.
        /// Required for IAS 21 currency revaluation compliance.
        /// </summary>
        MonthEnd = 3,

        /// <summary>
        /// Quarter-end closing rate for quarterly financial statements.
        /// </summary>
        QuarterEnd = 4,

        /// <summary>
        /// Year-end closing rate for annual financial statements.
        /// Required for fiscal year-end reporting.
        /// </summary>
        YearEnd = 5,

        /// <summary>
        /// Budget planning rate for forecasting and planning purposes.
        /// Set annually for budget preparation.
        /// </summary>
        Budget = 6,

        /// <summary>
        /// Fixed contractual rate for specific agreements or hedged positions.
        /// Used when rate is predetermined by contract or hedging instrument.
        /// </summary>
        Fixed = 7,

        /// <summary>
        /// Real-time spot rate (rarely stored, typically for reference).
        /// </summary>
        Spot = 8
    }

    /// <summary>
    /// Provider/bank perspective for a foreign-currency quote.
    /// </summary>
    public enum ExchangeRateQuoteSide
    {
        Mid = 1,
        Buying = 2,
        Selling = 3
    }

    /// <summary>
    /// Approval status for exchange rates requiring management review.
    /// </summary>
    public enum RateApprovalStatus
    {
        /// <summary>
        /// Rate pending approval - cannot be used in transactions yet.
        /// </summary>
        Pending = 1,

        /// <summary>
        /// Rate approved and available for use in transactions.
        /// </summary>
        Approved = 2,

        /// <summary>
        /// Rate rejected - cannot be used, requires re-entry or correction.
        /// </summary>
        Rejected = 3,

        /// <summary>
        /// Auto-approved based on variance threshold rules.
        /// No manual approval required.
        /// </summary>
        AutoApproved = 4
    }
}
