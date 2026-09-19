using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ErpSystem.Core.Entities.Base;

namespace ErpSystem.Core.Entities.Finance;

/// <summary>
/// Currency master entity for multi-currency financial operations.
/// Stores ISO 4217 compliant currency definitions, exchange rate configurations,
/// and formatting rules for display and transaction processing.
/// 
/// PATTERN: Currency Management Workflow
/// ======================================
/// - Currencies are pre-configured during system setup (typically 20-30 active currencies)
/// - Base currency (home currency) is designated and cannot be deleted
/// - Each currency defines rounding rules, decimal places, and display formats
/// - Inactive currencies can still appear in historical transactions but not for new entries
/// - Exchange rates link to this master table via CurrencyCode
/// 
/// INTEGRATION POINTS:
/// - ExchangeRate: Daily rates reference BaseCurrencyCode and TargetCurrencyCode
/// - Account: Multi-currency accounts link to currencies via AccountCurrencyLink
/// - AccountTransaction: Transactions reference TransactionCurrency
/// - JournalEntry: Multi-currency entries track PrimaryCurrency
/// </summary>
public class Currency : BusinessEntity
{
    // ========================================================================
    // CORE CURRENCY IDENTIFICATION (ISO 4217 Compliance)
    // ========================================================================
    
    /// <summary>
    /// ISO 4217 three-letter currency code (alphabetic code).
    /// This is the PRIMARY identifier for all currency references in the system.
    /// 
    /// Examples:
    /// - "GHS" - Ghana Cedi
    /// - "USD" - United States Dollar
    /// - "EUR" - Euro
    /// - "GBP" - British Pound Sterling
    /// - "NGN" - Nigerian Naira
    /// - "ZAR" - South African Rand
    /// 
    /// CRITICAL: This code MUST match ISO 4217 standard for international compliance.
    /// </summary>
    [Required]
    [MaxLength(3)]
    [MinLength(3)]
    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>
    /// Alias for CurrencyCode to satisfy interface requirements or legacy code
    /// </summary>
    [NotMapped]
    public string Code 
    { 
        get => CurrencyCode; 
        set => CurrencyCode = value; 
    }

    public int DisplayOrder { get; set; } = 0;


    /// <summary>
    /// ISO 4217 numeric currency code (3 digits).
    /// Provides alternative unique identifier for systems that prefer numeric codes.
    /// 
    /// Examples:
    /// - "936" - Ghana Cedi (GHS)
    /// - "840" - US Dollar (USD)
    /// - "978" - Euro (EUR)
    /// - "826" - British Pound (GBP)
    /// 
    /// Used for: SWIFT messages, banking integrations, ISO 20022 XML formats.
    /// </summary>
    [Required]
    [MaxLength(3)]
    [MinLength(3)]
    public string NumericCode { get; set; } = string.Empty;

    /// <summary>
    /// Full official currency name.
    /// 
    /// Examples:
    /// - "Ghana Cedi"
    /// - "United States Dollar"
    /// - "Euro"
    /// - "British Pound Sterling"
    /// 
    /// Used for: Reports, user interfaces, documentation.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string CurrencyName { get; set; } = string.Empty;

    /// <summary>
    /// Currency symbol for display purposes.
    /// 
    /// Examples:
    /// - "GH₵" or "₵" - Ghana Cedi
    /// - "$" - US Dollar
    /// - "€" - Euro
    /// - "£" - British Pound
    /// - "₦" - Nigerian Naira
    /// - "R" - South African Rand
    /// 
    /// NOTE: Some currencies share symbols (e.g., $ for USD, CAD, AUD).
    /// Always use CurrencyCode for unambiguous identification.
    /// </summary>
    [MaxLength(10)]
    public string? CurrencySymbol { get; set; }

    /// <summary>
    /// Plural form of the currency name for grammatically correct display.
    /// Example: "Ghana Cedis", "US Dollars", "Euros"
    /// </summary>
    [MaxLength(100)]
    public string? PluralName { get; set; }

    // ========================================================================
    // CURRENCY DECIMAL AND ROUNDING CONFIGURATION
    // ========================================================================
    
    /// <summary>
    /// Number of decimal places used for this currency.
    /// 
    /// Standard Values:
    /// - 2: Most currencies (USD, EUR, GBP, GHS) - e.g., $10.25
    /// - 0: Zero-decimal currencies (JPY, KRW) - e.g., ¥1000
    /// - 3: Some middle-eastern currencies (KWD, BHD, OMR) - e.g., 10.250 KWD
    /// 
    /// This determines:
    /// - Display formatting in UI
    /// - Database storage precision
    /// - Rounding behavior for calculations
    /// </summary>
    [Required]
    [Range(0, 4)]
    public int DecimalPlaces { get; set; } = 2;

    /// <summary>
    /// Rounding method applied to currency amounts.
    /// 
    /// Values:
    /// - "Standard" - Standard mathematical rounding (0.5 rounds up)
    /// - "Up" - Always round up (ceiling)
    /// - "Down" - Always round down (floor)
    /// - "ToNearest" - Round to nearest value based on RoundingPrecision
    /// - "BankersRounding" - Round to nearest even (IEEE 754 standard)
    /// 
    /// Default: "Standard" for most currencies.
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string RoundingMethod { get; set; } = "Standard";

    /// <summary>
    /// Rounding precision (smallest unit) for this currency.
    /// 
    /// Examples:
    /// - 0.01: Round to nearest cent (USD, EUR, GHS) - $10.24, $10.25
    /// - 0.05: Round to nearest 5 cents (some cash transactions) - $10.20, $10.25
    /// - 1.00: Round to nearest whole unit (JPY, KRW) - ¥1000
    /// - 0.001: Round to nearest mil (KWD) - 10.245 KWD
    /// 
    /// This is especially important for:
    /// - Cash transactions (some countries don't have 1-cent coins)
    /// - Tax calculations
    /// - Exchange rate conversions
    /// </summary>
    [Column(TypeName = "decimal(10,6)")]
    public decimal RoundingPrecision { get; set; } = 0.01m;

    // ========================================================================
    // DISPLAY AND FORMATTING RULES
    // ========================================================================
    
    /// <summary>
    /// Position of currency symbol relative to amount.
    /// 
    /// Values:
    /// - "Before" - Symbol before amount: $100.00, €100.00
    /// - "After" - Symbol after amount: 100.00₵, 100.00R
    /// - "BeforeWithSpace" - Symbol before with space: $ 100.00
    /// - "AfterWithSpace" - Symbol after with space: 100.00 €
    /// 
    /// Used for proper formatting in reports and user interfaces.
    /// </summary>
    [MaxLength(20)]
    public string SymbolPosition { get; set; } = "Before";

    /// <summary>
    /// Decimal separator character used for this currency.
    /// 
    /// Examples:
    /// - "." (period/dot) - US, UK, Ghana: 1,000.50
    /// - "," (comma) - Most of Europe: 1.000,50
    /// 
    /// Critical for:
    /// - Number parsing from user input
    /// - Report generation
    /// - Export/import formatting
    /// </summary>
    [Required]
    [MaxLength(1)]
    public string DecimalSeparator { get; set; } = ".";

    /// <summary>
    /// Thousands separator character (digit grouping).
    /// 
    /// Examples:
    /// - "," (comma) - US, UK, Ghana: 1,000,000.50
    /// - "." (period) - Most of Europe: 1.000.000,50
    /// - " " (space) - Some countries: 1 000 000.50
    /// - "" (none) - Some countries don't use thousands separator
    /// 
    /// Used for display formatting only, not for storage.
    /// </summary>
    [MaxLength(1)]
    public string? ThousandsSeparator { get; set; } = ",";

    /// <summary>
    /// Number of digits in each thousands group.
    /// 
    /// Standard Values:
    /// - 3: Most currencies (1,000,000) - groups of three
    /// - 2: Indian numbering system (10,00,000) - groups of two after first three
    /// 
    /// Default: 3 for most international currencies.
    /// </summary>
    [Range(2, 3)]
    public int DigitGrouping { get; set; } = 3;

    /// <summary>
    /// Sample format string showing how amounts display.
    /// Example: "GH₵ 1,234.56" or "$1,234.56" or "1.234,56 €"
    /// 
    /// Used for UI preview and documentation.
    /// Auto-generated based on symbol position and separators.
    /// </summary>
    [MaxLength(50)]
    public string? FormatExample { get; set; }

    // ========================================================================
    // CURRENCY CLASSIFICATION AND GROUPING
    // ========================================================================
    
    /// <summary>
    /// Indicates if this is the organization's base (home/functional) currency.
    /// 
    /// TRUE: This is the primary currency for financial reporting.
    ///       - All multi-currency transactions convert to this currency
    ///       - Financial statements prepared in this currency
    ///       - Only ONE currency can be marked as base currency
    ///       - Base currency CANNOT be deactivated or deleted
    /// 
    /// FALSE: This is a foreign currency for multi-currency transactions.
    /// 
    /// Example: For a Ghanaian company, GHS would be IsBaseCurrency = true.
    /// </summary>
    [Required]
    public bool IsBaseCurrency { get; set; } = false;

    /// <summary>
    /// Currency classification for grouping and filtering.
    /// 
    /// Values:
    /// - "Major" - Widely traded global currencies (USD, EUR, GBP, JPY, CHF)
    /// - "Regional" - Important regional currencies (GHS, NGN, ZAR, KES)
    /// - "Minor" - Less frequently traded currencies
    /// - "Crypto" - Cryptocurrencies (if supported) - BTC, ETH
    /// - "Commodity" - Commodity-backed or special currencies
    /// 
    /// Used for:
    /// - Currency picker UI (show major currencies first)
    /// - Exchange rate refresh priorities
    /// - Reporting categorization
    /// </summary>
    [MaxLength(20)]
    public string CurrencyClassification { get; set; } = "Regional";

    /// <summary>
    /// Geographic region where currency is primarily used.
    /// Examples: "West Africa", "Europe", "North America", "Asia Pacific"
    /// 
    /// Used for regional reporting and currency grouping.
    /// </summary>
    [MaxLength(50)]
    public string? GeographicRegion { get; set; }

    // ========================================================================
    // EXCHANGE RATE CONFIGURATION
    // ========================================================================
    
    /// <summary>
    /// Indicates if exchange rates should be automatically retrieved for this currency.
    /// 
    /// TRUE: System fetches daily rates from external APIs (ExchangeRatesAPI, XE, etc.)
    /// FALSE: Exchange rates entered manually by finance team
    /// 
    /// Typically TRUE for major currencies, FALSE for minor or custom currencies.
    /// </summary>
    public bool AutoRetrieveExchangeRate { get; set; } = false;

    /// <summary>
    /// Frequency of automatic exchange rate updates.
    /// Only relevant if AutoRetrieveExchangeRate = true.
    /// 
    /// Values:
    /// - "Daily" - Fetch rates once per business day (most common)
    /// - "Hourly" - Fetch rates every hour (for volatile currencies)
    /// - "RealTime" - Fetch rates on demand (rarely used due to API costs)
    /// - "Weekly" - Fetch rates weekly (for stable currencies)
    /// </summary>
    [MaxLength(20)]
    public string ExchangeRateUpdateFrequency { get; set; } = "Daily";

    /// <summary>
    /// Variance threshold percentage for exchange rate alerts.
    /// 
    /// When daily rate change exceeds this threshold, system generates alert:
    /// - For manual review
    /// - For treasury/finance team notification
    /// - For risk management assessment
    /// 
    /// Example: 5.0 = Alert if rate changes more than 5% in one day.
    /// NULL = No automatic alerts.
    /// </summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal? RateVarianceThresholdPercentage { get; set; } = 5.0m;

    /// <summary>
    /// Legacy schema value retained for backward-compatible database reads only.
    /// It is intentionally not exposed as transaction policy: operational rate type is
    /// governed by the effective account-currency link, with tenant policy fallbacks.
    /// </summary>
    [MaxLength(20)]
    public string DefaultRateType { get; set; } = "Daily";

    // ========================================================================
    // STATUS AND ACTIVATION
    // ========================================================================
    
    /// <summary>
    /// Indicates if currency is currently active for use in new transactions.
    /// 
    /// ACTIVE (TRUE):
    /// - Available in currency selection dropdowns
    /// - Can be linked to GL accounts
    /// - Can be used in new transactions
    /// - Exchange rates retrieved/updated
    /// 
    /// INACTIVE (FALSE):
    /// - Not available for new transactions or account links
    /// - Historical transactions remain intact and reportable
    /// - Exchange rates not updated
    /// - Still visible in historical reports and inquiries
    /// 
    /// Use cases for inactivation:
    /// - Currency discontinued (e.g., legacy European currencies pre-Euro)
    /// - Organization no longer trades in this currency
    /// - Temporary suspension due to economic sanctions
    /// </summary>
    [Required]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Date when currency became active in the system.
    /// Transactions cannot be dated before this date in this currency.
    /// </summary>
    public DateTime ActivationDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date when currency was deactivated (if inactive).
    /// NULL if currency is currently active.
    /// </summary>
    public DateTime? DeactivationDate { get; set; }

    /// <summary>
    /// Reason for currency deactivation.
    /// Required when IsActive = false.
    /// 
    /// Examples:
    /// - "No longer trading with suppliers in this currency"
    /// - "Currency replaced by Euro"
    /// - "Economic sanctions prohibit transactions"
    /// </summary>
    [MaxLength(500)]
    public string? DeactivationReason { get; set; }

    // ========================================================================
    // USAGE STATISTICS AND TRACKING
    // ========================================================================
    
    /// <summary>
    /// Indicates if any GL accounts are linked to this currency.
    /// 
    /// TRUE: Currency is linked to one or more accounts
    /// FALSE: No account linkages exist
    /// 
    /// Important for:
    /// - Preventing deletion of currencies in use
    /// - Usage reporting and analytics
    /// </summary>
    public bool HasAccountLinkages { get; set; } = false;

    /// <summary>
    /// Count of GL accounts linked to this currency.
    /// Updated automatically when AccountCurrencyLink records are created/deleted.
    /// </summary>
    public int AccountLinkageCount { get; set; } = 0;

    /// <summary>
    /// Indicates if any transactions have been posted in this currency.
    /// 
    /// TRUE: Currency has transaction history
    /// FALSE: No transactions posted yet
    /// 
    /// Once TRUE, currency cannot be deleted (only deactivated).
    /// This preserves historical data integrity.
    /// </summary>
    public bool HasTransactionHistory { get; set; } = false;

    /// <summary>
    /// Count of transactions posted in this currency.
    /// Updated automatically by transaction posting process.
    /// </summary>
    public int TransactionCount { get; set; } = 0;

    /// <summary>
    /// Date of first transaction posted in this currency.
    /// NULL if no transactions have been posted.
    /// </summary>
    public DateTime? FirstTransactionDate { get; set; }

    /// <summary>
    /// Date of most recent transaction posted in this currency.
    /// NULL if no transactions have been posted.
    /// Used for:
    /// - Identifying dormant currencies
    /// - Currency usage analytics
    /// </summary>
    public DateTime? LastTransactionDate { get; set; }

    // ========================================================================
    // COUNTRY AND LEGAL INFORMATION
    // ========================================================================
    
    /// <summary>
    /// Primary country where this currency is legal tender.
    /// ISO 3166-1 alpha-2 country code (two letters).
    /// 
    /// Examples:
    /// - "GH" - Ghana (GHS)
    /// - "US" - United States (USD)
    /// - "GB" - United Kingdom (GBP)
    /// - "NG" - Nigeria (NGN)
    /// 
    /// NOTE: Some currencies used in multiple countries (EUR in Eurozone).
    /// This indicates the primary or issuing country.
    /// </summary>
    [MaxLength(2)]
    public string? CountryCode { get; set; }

    /// <summary>
    /// Primary country name for display purposes.
    /// Example: "Ghana", "United States", "United Kingdom"
    /// </summary>
    [MaxLength(100)]
    public string? CountryName { get; set; }

    /// <summary>
    /// List of all countries where this currency is legal tender.
    /// Comma-separated country codes.
    /// 
    /// Example for EUR: "AT,BE,CY,EE,FI,FR,DE,GR,IE,IT,LV,LT,LU,MT,NL,PT,SK,SI,ES"
    /// </summary>
    [MaxLength(500)]
    public string? CountriesUsingCurrency { get; set; }

    /// <summary>
    /// Central bank or monetary authority responsible for this currency.
    /// 
    /// Examples:
    /// - "Bank of Ghana"
    /// - "Federal Reserve System" (USD)
    /// - "European Central Bank" (EUR)
    /// - "Central Bank of Nigeria"
    /// 
    /// Used for:
    /// - Official exchange rate source identification
    /// - Regulatory compliance documentation
    /// </summary>
    [MaxLength(200)]
    public string? CentralBank { get; set; }

    // ========================================================================
    // CURRENCY SUBDIVISION (Minor Units)
    // ========================================================================
    
    /// <summary>
    /// Name of the minor unit (subunit) of currency.
    /// 
    /// Examples:
    /// - "Pesewa" (subdivision of Ghana Cedi) - 100 pesewa = 1 cedi
    /// - "Cent" (subdivision of USD, EUR, ZAR) - 100 cents = 1 dollar/euro/rand
    /// - "Pence" (subdivision of GBP) - 100 pence = 1 pound
    /// - "Kobo" (subdivision of NGN) - 100 kobo = 1 naira
    /// - NULL for currencies with no subdivision (JPY, KRW)
    /// 
    /// Used for proper wording in financial documents and checks.
    /// </summary>
    [MaxLength(50)]
    public string? MinorUnitName { get; set; }

    /// <summary>
    /// Plural form of minor unit name.
    /// Example: "Pesewas", "Cents", "Pence", "Kobo"
    /// </summary>
    [MaxLength(50)]
    public string? MinorUnitPluralName { get; set; }

    /// <summary>
    /// Number of minor units that make up one major unit.
    /// 
    /// Standard Values:
    /// - 100: Most currencies (100 cents = $1, 100 pesewa = GH₵1)
    /// - 1: No subdivision (JPY, KRW)
    /// - 1000: Some currencies (1000 fils = 1 dinar for some Middle Eastern currencies)
    /// 
    /// Formula: 1 Major Unit = MinorUnitRatio × Minor Units
    /// </summary>
    [Range(1, 1000)]
    public int MinorUnitRatio { get; set; } = 100;

    // ========================================================================
    // NOTES AND ADDITIONAL INFORMATION
    // ========================================================================
    
    /// <summary>
    /// Additional notes or special handling instructions for this currency.
    /// 
    /// Examples:
    /// - "Subject to frequent volatility - review exchange rates daily"
    /// - "Requires approval for transactions over $100,000"
    /// - "Historical currency - replaced by Euro in 2002"
    /// - "Sanctions apply - consult legal before transactions"
    /// </summary>
    [MaxLength(1000)]
    public string? Notes { get; set; }

    /// <summary>
    /// Flag indicating if currency has special restrictions or requirements.
    /// Examples: Capital controls, sanctions, regulatory restrictions.
    /// </summary>
    public bool HasRestrictions { get; set; } = false;

    /// <summary>
    /// Description of restrictions or special requirements.
    /// Example: "Transactions require central bank approval", "Subject to capital controls"
    /// </summary>
    [MaxLength(500)]
    public string? RestrictionsDescription { get; set; }

    // ========================================================================
    // HISTORICAL TRACKING (For Currency Redenominations)
    // ========================================================================
    
    /// <summary>
    /// Indicates if this currency has been redenominated (revalued).
    /// 
    /// Example: Ghana Cedi was redenominated in 2007:
    /// - Old Cedi (GHC): 10,000 GHC = 1 New Cedi (GHS)
    /// - Redenomination ratio: 10,000:1
    /// 
    /// TRUE: Currency underwent redenomination
    /// FALSE: No redenomination history
    /// </summary>
    public bool HasBeenRedenominated { get; set; } = false;

    /// <summary>
    /// Date of redenomination (if applicable).
    /// Example: Ghana Cedi redenomination date: July 1, 2007
    /// </summary>
    public DateTime? RedenominationDate { get; set; }

    /// <summary>
    /// Redenomination ratio (old:new).
    /// Example: 10000 (means 10,000 old units = 1 new unit)
    /// NULL if never redenominated.
    /// </summary>
    [Column(TypeName = "decimal(18,6)")]
    public decimal? RedenominationRatio { get; set; }

    /// <summary>
    /// Previous currency code before redenomination.
    /// Example: "GHC" (old Ghana Cedi before 2007)
    /// NULL if never redenominated.
    /// </summary>
    [MaxLength(3)]
    public string? PreviousCurrencyCode { get; set; }

    /// <summary>
    /// Notes about redenomination for historical reference.
    /// Example: "Redenominated in 2007 - 10,000 GHC = 1 GHS"
    /// </summary>
    [MaxLength(500)]
    public string? RedenominationNotes { get; set; }

    // ========================================================================
    // NAVIGATION PROPERTIES
    // ========================================================================
    
    /// <summary>
    /// Collection of exchange rates for this currency.
    /// Links to ExchangeRate where TargetCurrencyCode matches this CurrencyCode.
    /// </summary>
    public virtual ICollection<ExchangeRate> ExchangeRates { get; set; } = new List<ExchangeRate>();

    /// <summary>
    /// Collection of account currency links using this currency.
    /// Shows which GL accounts can transact in this currency.
    /// </summary>
    public virtual ICollection<AccountCurrencyLink> AccountCurrencyLinks { get; set; } = new List<AccountCurrencyLink>();
}
