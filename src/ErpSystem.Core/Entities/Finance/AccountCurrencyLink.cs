using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;

namespace ErpSystem.Core.Entities.Finance
{
    /// <summary>
    /// Defines currency linkages for multi-currency general ledger accounts.
    /// 
    /// CRITICAL FEATURE: Currency Link Protection
    /// ========================================
    /// Once an account has transaction history in a specific currency, that currency
    /// link CANNOT be removed. This prevents corruption of historical data and ensures
    /// foreign currency sub-ledger integrity.
    /// 
    /// Example Scenario:
    /// - Account "1010-Cash-USD" is configured to accept USD transactions
    /// - System posts 5 USD transactions totaling $10,000
    /// - User attempts to remove USD currency link
    /// - System BLOCKS the removal because transaction history exists
    /// - User can only mark currency as INACTIVE (prevents new transactions but preserves history)
    /// 
    /// This implements the "Currency Link Protection (Transaction History Rule)" from
    /// Finance Module Enhanced Workflows specification.
    /// </summary>
    public class AccountCurrencyLink : BusinessEntity
    {
        #region Core Linkage Properties

        /// <summary>
        /// Reference to the general ledger account this currency link belongs to.
        /// </summary>
        [Required]
        public Guid AccountId { get; set; }

        /// <summary>
        /// Currency code linked to this account (ISO 4217 three-letter code).
        /// Examples: "USD", "EUR", "GBP", "NGN"
        /// 
        /// NOTE: Base currency (GHS) is automatically linked when account is created
        /// and cannot be removed.
        /// </summary>
        [Required]
        [MaxLength(3)]
        public string LinkedCurrencyCode { get; set; } = string.Empty;

        /// <summary>
        /// Frequency of automatic currency revaluation when the effective
        /// account/book/currency policy includes this exposure.
        /// </summary>
        public RevaluationFrequency RevaluationFrequency { get; set; } = RevaluationFrequency.Monthly;

        #endregion

        #region Exchange Rate Configuration

        /// <summary>
        /// Type of exchange rate used for normal transactions in this currency.
        /// 
        /// Options:
        /// - Daily: Use daily spot rate from ExchangeRates table (transaction date)
        /// - Average: Use monthly/quarterly average rate for smoothing
        /// - Budget: Use budgeted rate for planning purposes
        /// - Fixed: Use predetermined rate (for controlled scenarios)
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string TransactionRateType { get; set; } = "Daily";

        /// <summary>
        /// Default provider/bank quote side for ordinary GL transactions.
        /// Subledger document policies remain authoritative for AR and AP.
        /// </summary>
        public ExchangeRateQuoteSide TransactionQuoteSide { get; set; } = ExchangeRateQuoteSide.Mid;

        /// <summary>
        /// Type of exchange rate used for period-end revaluation.
        /// 
        /// Typically "Month-End" or "Year-End" to comply with IAS 21 requirements.
        /// Period-end rates are closing spot rates at the balance sheet date.
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string RevaluationRateType { get; set; } = "Month-End";

        /// <summary>
        /// Closing/revaluation should normally use the neutral mid quote.
        /// </summary>
        public ExchangeRateQuoteSide RevaluationQuoteSide { get; set; } = ExchangeRateQuoteSide.Mid;

        #endregion

        #region Status and Activation

        /// <summary>
        /// Indicates if this currency link is currently active.
        /// 
        /// ACTIVE (TRUE):
        /// - Currency available for new transactions
        /// - Eligible for revaluation only when its exact account/book/currency policy includes it
        /// - Appears in currency selection dropdowns
        /// 
        /// INACTIVE (FALSE):
        /// - Currency NOT available for new transactions
        /// - Excluded from revaluation process
        /// - Historical transactions remain intact and reportable
        /// - Balance inquiry still shows currency (with 'Inactive' label)
        /// 
        /// IMPORTANT: Inactivation is the ONLY way to "remove" a currency link
        /// that has transaction history. This preserves data integrity.
        /// </summary>
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Date when currency link became effective for transactions.
        /// Transactions dated before this date cannot use this currency for this account.
        /// </summary>
        public DateTime EffectiveDate { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Date when currency link was ended/inactivated.
        /// NULL if currency link is currently active.
        /// 
        /// When set:
        /// - IsActive automatically becomes FALSE
        /// - New transactions cannot use this currency after this date
        /// - Historical transactions remain queryable and reportable
        /// </summary>
        public DateTime? EffectiveEndDate { get; set; }

        /// <summary>
        /// Reason for currency link inactivation.
        /// Required when setting EffectiveEndDate / marking IsActive = false.
        /// 
        /// Examples:
        /// - "Currency no longer needed for operations"
        /// - "Vendor switched to GHS billing"
        /// - "Bank account closed"
        /// </summary>
        [MaxLength(500)]
        public string? InactivationReason { get; set; }

        #endregion

        #region Transaction History Tracking

        /// <summary>
        /// Indicates if any transactions have been posted in this currency.
        /// 
        /// This flag is CRITICAL for currency link protection:
        /// - FALSE: Currency link can be physically deleted (no history exists)
        /// - TRUE: Currency link can ONLY be inactivated, NOT deleted
        /// 
        /// Automatically set to TRUE when first transaction posts.
        /// </summary>
        public bool HasTransactionHistory { get; set; } = false;

        /// <summary>
        /// Count of transactions posted using this currency link.
        /// Used for quick validation and reporting.
        /// Updated automatically by transaction posting process.
        /// </summary>
        public int TransactionCount { get; set; } = 0;

        /// <summary>
        /// Date of first transaction posted in this currency.
        /// NULL if no transactions have been posted yet.
        /// </summary>
        public DateTime? FirstTransactionDate { get; set; }

        /// <summary>
        /// Date of most recent transaction posted in this currency.
        /// NULL if no transactions have been posted yet.
        /// </summary>
        public DateTime? LastTransactionDate { get; set; }

        #endregion

        #region Balance Tracking

        /// <summary>
        /// Current balance in the foreign currency (original currency amounts).
        /// 
        /// Example: If account is "Cash-USD" with 5 transactions totaling $10,000,
        /// this field shows 10000.00 (in USD).
        /// 
        /// Updated automatically by transaction posting.
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal ForeignCurrencyBalance { get; set; } = 0;

        /// <summary>
        /// Current balance converted to base currency (GHS) using latest applicable rate.
        /// 
        /// Calculation:
        /// - ForeignCurrencyBalance × Current Exchange Rate = BaseCurrencyEquivalent
        /// 
        /// Example: $10,000 USD × 15.25 GHS/USD = 152,500 GHS
        /// 
        /// Recalculated during:
        /// - Transaction posting (using transaction date rate)
        /// - Currency revaluation (using period-end rate per IAS 21)
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal BaseCurrencyEquivalent { get; set; } = 0;

        /// <summary>
        /// Exchange rate used for most recent balance calculation.
        /// Provides transparency and audit trail for balance conversions.
        /// </summary>
        [Column(TypeName = "decimal(10,6)")]
        public decimal CurrentExchangeRate { get; set; } = 0;

        /// <summary>
        /// Date of exchange rate used for current balance calculation.
        /// Critical for revaluation process and audit purposes.
        /// </summary>
        public DateTime? RateEffectiveDate { get; set; }

        #endregion

        #region Revaluation History

        /// <summary>
        /// Date of last currency revaluation performed for this currency link.
        /// NULL if no revaluation has been performed yet.
        /// 
        /// Revaluation adjusts BaseCurrencyEquivalent to reflect current exchange rates
        /// without changing ForeignCurrencyBalance (per IAS 21).
        /// </summary>
        public DateTime? LastRevaluationDate { get; set; }

        /// <summary>
        /// Amount of unrealized gain/loss from most recent revaluation.
        /// 
        /// Positive = Unrealized Gain (favorable exchange rate movement)
        /// Negative = Unrealized Loss (unfavorable exchange rate movement)
        /// 
        /// Example:
        /// - Original: $10,000 USD @ 15.00 = 150,000 GHS
        /// - Revaluation: $10,000 USD @ 15.50 = 155,000 GHS
        /// - Unrealized Gain = 5,000 GHS
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal LastRevaluationAdjustment { get; set; } = 0;

        /// <summary>
        /// Cumulative unrealized gain/loss from all revaluations to date.
        /// 
        /// Tracks total impact of exchange rate fluctuations over the account's lifetime.
        /// Useful for financial analysis and risk management reporting.
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal CumulativeRevaluationAdjustment { get; set; } = 0;

        #endregion

        #region Metadata and Audit

        /// <summary>
        /// User ID who created this currency link.
        /// Captured for audit trail and accountability.
        /// </summary>
        public Guid CreatedByUserId { get; set; }

        /// <summary>
        /// Timestamp when currency link was created.
        /// </summary>
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// User ID who last modified this currency link.
        /// NULL if no modifications since creation.
        /// </summary>
        public Guid? ModifiedByUserId { get; set; }

        /// <summary>
        /// Timestamp of last modification.
        /// NULL if no modifications since creation.
        /// </summary>
        public DateTime? ModifiedDate { get; set; }

        /// <summary>
        /// Additional notes or comments about this currency link.
        /// Example: "USD link added for international supplier payments"
        /// </summary>
        [MaxLength(1000)]
        public string? Notes { get; set; }

        #endregion

        #region Navigation Properties

        /// <summary>
        /// The general ledger account this currency link belongs to.
        /// </summary>
        [ForeignKey(nameof(AccountId))]
        public virtual Account Account { get; set; } = null!;

        public virtual ICollection<AccountBookCurrencyPolicy> BookPolicies { get; set; } = new List<AccountBookCurrencyPolicy>();

        #endregion
    }

    /// <summary>
    /// Currency revaluation frequency options per IAS 21 compliance.
    /// </summary>
    public enum RevaluationFrequency
    {
        /// <summary>
        /// Monthly revaluation at end of each calendar month.
        /// Most common for active trading currencies.
        /// </summary>
        Monthly = 1,

        /// <summary>
        /// Quarterly revaluation at end of fiscal quarters.
        /// Suitable for moderate currency exposure.
        /// </summary>
        Quarterly = 2,

        /// <summary>
        /// Annual revaluation at fiscal year-end only.
        /// Minimum requirement for IFRS compliance.
        /// </summary>
        Annually = 3,

        /// <summary>
        /// Ad-hoc revaluation triggered manually by finance team.
        /// Used for special situations or interim reporting.
        /// </summary>
        AdHoc = 4,

        /// <summary>
        /// No automatic revaluation scheduled.
        /// Used when no automatic revaluation schedule applies.
        /// </summary>
        None = 0
    }
}

        ///
