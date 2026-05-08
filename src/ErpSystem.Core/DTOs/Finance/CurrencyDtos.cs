using System;
using System.ComponentModel.DataAnnotations;

namespace ErpSystem.Core.DTOs.Finance
{
    /// <summary>
    /// Shared lightweight reference for the tenant's base currency.
    /// Used across the API when only the canonical code/name/symbol metadata is needed.
    /// </summary>
    public class BaseCurrencyReferenceDto
    {
        public string CurrencyCode { get; set; } = "GHS";
        public string CurrencyName { get; set; } = "Ghana Cedi";
        public string CurrencySymbol { get; set; } = "₵";
        public int DecimalPlaces { get; set; } = 2;
    }

    /// <summary>
    /// READ DTO: Represents a Currency as returned by the Finance API.
    /// Used for multi-currency operations across all modules.
    /// </summary>
    public class CurrencyDto
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        
        /// <summary>ISO 4217 three-letter currency code (e.g., "GHS", "USD", "EUR")</summary>
        public string CurrencyCode { get; set; } = string.Empty;
        
        /// <summary>ISO 4217 numeric currency code (3 digits)</summary>
        public string NumericCode { get; set; } = string.Empty;
        
        /// <summary>Full official currency name (e.g., "Ghana Cedi")</summary>
        public string CurrencyName { get; set; } = string.Empty;
        
        /// <summary>Currency symbol for display (e.g., "GH₵", "$", "€")</summary>
        public string? CurrencySymbol { get; set; }
        
        /// <summary>Plural form of currency name</summary>
        public string? PluralName { get; set; }
        
        /// <summary>Number of decimal places (0-4, typically 2)</summary>
        public int DecimalPlaces { get; set; }
        
        /// <summary>Rounding method: "Standard", "Up", "Down", "ToNearest", "BankersRounding"</summary>
        public string RoundingMethod { get; set; } = "Standard";
        
        /// <summary>Rounding precision (e.g., 0.01 for cents)</summary>
        public decimal RoundingPrecision { get; set; }
        
        /// <summary>Symbol position: "Before", "After", "BeforeWithSpace", "AfterWithSpace"</summary>
        public string SymbolPosition { get; set; } = "Before";
        
        /// <summary>Decimal separator character ("." or ",")</summary>
        public string DecimalSeparator { get; set; } = ".";
        
        /// <summary>Thousands separator character (",", ".", or " ")</summary>
        public string? ThousandsSeparator { get; set; }
        
        /// <summary>Number of digits in thousands group (2 or 3)</summary>
        public int DigitGrouping { get; set; }
        
        /// <summary>TRUE if this is the organization's base currency</summary>
        public bool IsBaseCurrency { get; set; }
        
        /// <summary>Currency classification: "Major", "Regional", "Minor", "Crypto"</summary>
        public string CurrencyClassification { get; set; } = "Regional";
        
        /// <summary>Geographic region (e.g., "West Africa", "Europe")</summary>
        public string? GeographicRegion { get; set; }
        
        /// <summary>TRUE if exchange rates auto-retrieved from external APIs</summary>
        public bool AutoRetrieveExchangeRate { get; set; }
        
        /// <summary>Update frequency: "Daily", "Hourly", "RealTime", "Weekly"</summary>
        public string ExchangeRateUpdateFrequency { get; set; } = "Daily";
        
        /// <summary>Default rate type: "Daily", "Average", "MonthEnd", "Budget", "Fixed"</summary>
        public string DefaultRateType { get; set; } = "Daily";
        
        /// <summary>TRUE if currency is active for new transactions</summary>
        public bool IsActive { get; set; }
        
        /// <summary>Date when currency became active</summary>
        public DateTime ActivationDate { get; set; }
        
        /// <summary>Date when currency was deactivated (if inactive)</summary>
        public DateTime? DeactivationDate { get; set; }
        
        /// <summary>TRUE if any GL accounts are linked to this currency</summary>
        public bool HasAccountLinkages { get; set; }
        
        /// <summary>Count of GL accounts linked to this currency</summary>
        public int AccountLinkageCount { get; set; }
        
        /// <summary>TRUE if any transactions posted in this currency</summary>
        public bool HasTransactionHistory { get; set; }
        
        /// <summary>Count of transactions in this currency</summary>
        public int TransactionCount { get; set; }
        
        /// <summary>Primary country code (ISO 3166-1 alpha-2)</summary>
        public string? CountryCode { get; set; }
        
        /// <summary>Primary country name</summary>
        public string? CountryName { get; set; }
        
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public string? CreatedBy { get; set; }
        public string? UpdatedBy { get; set; }
    }

    /// <summary>
    /// CREATE DTO: Payload for creating a new currency.
    /// Used by Finance module during setup.
    /// </summary>
    public class CreateCurrencyDto
    {
        [Required]
        [MaxLength(3)]
        [MinLength(3)]
        public string CurrencyCode { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(3)]
        [MinLength(3)]
        public string NumericCode { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(100)]
        public string CurrencyName { get; set; } = string.Empty;
        
        [MaxLength(10)]
        public string? CurrencySymbol { get; set; }
        
        [MaxLength(100)]
        public string? PluralName { get; set; }
        
        [Required]
        [Range(0, 4)]
        public int DecimalPlaces { get; set; } = 2;
        
        [Required]
        [MaxLength(20)]
        public string RoundingMethod { get; set; } = "Standard";
        
        public decimal RoundingPrecision { get; set; } = 0.01m;
        
        [MaxLength(20)]
        public string SymbolPosition { get; set; } = "Before";
        
        [Required]
        [MaxLength(1)]
        public string DecimalSeparator { get; set; } = ".";
        
        [MaxLength(1)]
        public string? ThousandsSeparator { get; set; } = ",";
        
        [Range(2, 3)]
        public int DigitGrouping { get; set; } = 3;
        
        public bool IsBaseCurrency { get; set; } = false;
        
        [MaxLength(20)]
        public string CurrencyClassification { get; set; } = "Regional";
        
        [MaxLength(50)]
        public string? GeographicRegion { get; set; }
        
        public bool AutoRetrieveExchangeRate { get; set; } = false;
        
        [MaxLength(20)]
        public string ExchangeRateUpdateFrequency { get; set; } = "Daily";
        
        [MaxLength(20)]
        public string DefaultRateType { get; set; } = "Daily";
        
        public bool IsActive { get; set; } = true;
        
        [MaxLength(2)]
        public string? CountryCode { get; set; }
        
        [MaxLength(100)]
        public string? CountryName { get; set; }
    }

    /// <summary>
    /// UPDATE DTO: Payload for updating an existing currency.
    /// </summary>
    public class UpdateCurrencyDto
    {
        [Required]
        [MaxLength(100)]
        public string CurrencyName { get; set; } = string.Empty;
        
        [MaxLength(10)]
        public string? CurrencySymbol { get; set; }
        
        [MaxLength(100)]
        public string? PluralName { get; set; }
        
        [MaxLength(20)]
        public string RoundingMethod { get; set; } = "Standard";
        
        public decimal RoundingPrecision { get; set; } = 0.01m;
        
        [MaxLength(20)]
        public string SymbolPosition { get; set; } = "Before";
        
        [MaxLength(1)]
        public string? ThousandsSeparator { get; set; }
        
        [Range(2, 3)]
        public int DigitGrouping { get; set; } = 3;
        
        [MaxLength(20)]
        public string CurrencyClassification { get; set; } = "Regional";
        
        [MaxLength(50)]
        public string? GeographicRegion { get; set; }
        
        public bool AutoRetrieveExchangeRate { get; set; } = false;
        
        [MaxLength(20)]
        public string ExchangeRateUpdateFrequency { get; set; } = "Daily";
        
        [MaxLength(20)]
        public string DefaultRateType { get; set; } = "Daily";
        
        public bool IsActive { get; set; } = true;
    }
}
